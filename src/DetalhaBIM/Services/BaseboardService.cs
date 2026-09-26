using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class BaseboardOptions
    {
        /// <summary>Verdadeiro = perfil de parede (Wall Sweep); falso = parede fina de rodapé.</summary>
        public bool UseSweep { get; set; }
        public ElementId WallTypeId { get; set; }
        public ElementId SweepTypeId { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        /// <summary>Deslocamento da base em relação ao piso do ambiente (pés).</summary>
        public double BaseOffset { get; set; }
        /// <summary>Apoia o rodapé sobre o piso modelado dentro do ambiente, quando houver.</summary>
        public bool OnFloor { get; set; } = true;
        public bool CutAtDoors { get; set; } = true;
        /// <summary>Também interrompe em janelas/portas-janelas com peitoril abaixo do topo do rodapé.</summary>
        public bool CutAtLowWindows { get; set; } = true;
        /// <summary>Contorna pilares e demais elementos delimitadores (não só paredes).</summary>
        public bool AroundColumns { get; set; } = true;
        public bool SkipExisting { get; set; } = true;
    }

    /// <summary>
    /// Rodapés automáticos pelo contorno de acabamento dos ambientes, interrompidos nas portas.
    /// Método principal: paredes finas (não delimitadoras de ambiente) com cantos resolvidos;
    /// alternativo: perfil de parede (Wall Sweep) do tipo escolhido na face voltada ao ambiente.
    /// </summary>
    public class BaseboardService
    {
        public const string Marker = "RODAPÉ";

        private readonly Document _doc;
        private readonly Report _report;
        private readonly HashSet<string> _sweepDone = new HashSet<string>();
        private readonly FaceFinder _faces = new FaceFinder(null);
        private List<Floor> _floors;

        public BaseboardService(Document doc, Report report)
        {
            _doc = doc;
            _report = report;
        }

        /// <summary>Comprimento total de rodapé criado (pés).</summary>
        public double Length { get; private set; }

        private class Piece
        {
            public Curve Curve;
            public Element Host;
            public XYZ Inward;
            public bool Active;
            public List<(double a, double b)> Cuts = new List<(double, double)>();
        }

        public static string Comment(Room room) => Marker + " - " + RoomGeo.Label(room);

        /// <summary>Cria os rodapés de um ambiente. Deve ser chamado dentro de uma transação.</summary>
        public int Create(Room room, BaseboardOptions o)
        {
            if (o.SkipExisting && HasBaseboards(room))
            {
                _report.Count("ambientes que já tinham rodapé (ignorados)");
                return 0;
            }
            double baseZ = FloorTop(room, o) + o.BaseOffset;
            int created = 0;
            foreach (IList<BoundarySegment> loop in RoomGeo.Segments(room))
            {
                List<Piece> pieces = Pieces(room, loop, o);
                created += o.UseSweep ? Sweeps(room, pieces, o, baseZ) : Walls(room, pieces, o, baseZ);
            }
            return created;
        }

        private bool HasBaseboards(Room room)
        {
            string c = Comment(room);
            return new FilteredElementCollector(_doc).OfClass(typeof(Wall)).Cast<Wall>()
                .Any(w => w.LevelId == room.LevelId && Q.Text(w, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) == c);
        }

        /// <summary>Topo do piso acabado dentro do ambiente (ou a base do ambiente).</summary>
        private double FloorTop(Room room, BaseboardOptions o)
        {
            double baseZ = RoomGeo.BaseElevation(room);
            if (!o.OnFloor) return baseZ;
            XYZ p = RoomGeo.Point(room);
            if (p == null) return baseZ;
            _floors ??= Q.All<Floor>(_doc);
            double best = double.MinValue;
            foreach (Floor fl in _floors)
            {
                BoundingBoxXYZ bb = fl.get_BoundingBox(null);
                if (bb == null || p.X < bb.Min.X || p.X > bb.Max.X || p.Y < bb.Min.Y || p.Y > bb.Max.Y) continue;
                if (bb.Max.Z < baseZ - Conv.Cm(30) || bb.Max.Z > baseZ + Conv.Cm(30)) continue;
                if (!RoomGeo.Contains(room, (bb.Min + bb.Max) / 2) && !Covers(fl, p)) continue;
                best = Math.Max(best, bb.Max.Z);
            }
            return best > double.MinValue ? best : baseZ;
        }

        private static bool Covers(Floor fl, XYZ p)
        {
            try
            {
                ElementScan scan = ElementScan.Of(fl, null);
                return scan.TopFaceAt(p, double.MaxValue) != null;
            }
            catch
            {
                return false;
            }
        }

        // ================================================================== contorno

        private List<Piece> Pieces(Room room, IList<BoundarySegment> loop, BaseboardOptions o)
        {
            var list = new List<Piece>();
            foreach (BoundarySegment seg in loop)
            {
                Curve c = seg.GetCurve();
                if (c == null || c.Length < Conv.Mm(5)) continue;
                Element e = _doc.GetElement(seg.ElementId);
                bool linked = e == null && seg.LinkElementId != ElementId.InvalidElementId;
                bool active = e is Wall || linked || (o.AroundColumns && e != null && !(e is CurveElement));
                var piece = new Piece { Curve = c, Host = e, Active = active, Inward = Inward(room, c) };
                if (linked) _report.Warn("Paredes de modelos vinculados recebem rodapé, mas as portas delas não são detectadas.");
                if (active && e is Wall wall && c is Line line) piece.Cuts = Cuts(wall, line, o);
                list.Add(piece);
            }
            return list;
        }

        /// <summary>Normal horizontal apontando para dentro do ambiente, no meio do segmento.</summary>
        private static XYZ Inward(Room room, Curve c)
        {
            XYZ mid = c.Evaluate(0.5, true);
            XYZ tan = c.ComputeDerivatives(0.5, true).BasisX;
            XYZ n = Geo.LeftNormal(tan);
            if (n.IsZeroLength()) return XYZ.Zero;
            return RoomGeo.Contains(room, mid + n * Conv.Cm(3)) ? n : n.Negate();
        }

        /// <summary>Trechos (posições ao longo do segmento) ocupados por portas e janelas baixas.</summary>
        private List<(double, double)> Cuts(Wall wall, Line seg, BaseboardOptions o)
        {
            var cuts = new List<(double, double)>();
            if (!o.CutAtDoors && !o.CutAtLowWindows) return cuts;
            XYZ t = Geo.FlatDir(seg.Direction);
            double s0 = seg.GetEndPoint(0).DotProduct(t), s1 = seg.GetEndPoint(1).DotProduct(t);
            double lo = Math.Min(s0, s1), hi = Math.Max(s0, s1);
            List<FaceInfo> jambs = null;

            foreach (FamilyInstance fi in Q.OpeningsIn(_doc, new[] { wall.Id }))
            {
                bool door = fi.Category?.BuiltInCategory == BuiltInCategory.OST_Doors;
                if (door && !o.CutAtDoors) continue;
                if (!door)
                {
                    double sill = Q.Double(fi, BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM) ?? double.MaxValue;
                    if (!o.CutAtLowWindows || sill > o.BaseOffset + o.Height) continue;
                }
                XYZ p = Geo.ElementPoint(fi);
                if (p == null) continue;
                // A esquadria precisa estar neste trecho do contorno (e não em outra face da parede).
                if (Geo.Flat(p - seg.Project(p).XYZPoint).GetLength() > wall.Width + Conv.Cm(5)) continue;
                double c = p.DotProduct(t);
                if (c < lo - Conv.Cm(1) || c > hi + Conv.Cm(1)) continue;

                // Vão real: ombreiras da parede próximas à esquadria; senão, a largura da família.
                double half = (Q.OpeningWidth(fi) ?? Conv.Cm(80)) / 2;
                jambs ??= _faces.FacesNormalTo(wall, t);
                List<double> near = jambs.Select(f => f.PositionAlong(t))
                    .Where(v => Math.Abs(v - c) <= half + Conv.Cm(8)).ToList();
                double a = near.Count >= 2 ? near.Min() : c - half;
                double b = near.Count >= 2 ? near.Max() : c + half;
                if (near.Count >= 2 && (b - a) > 2 * half + Conv.Cm(15))
                {
                    a = c - half;
                    b = c + half;
                }
                cuts.Add((a, b));
            }
            return cuts;
        }

        // ================================================================== paredes finas

        private int Walls(Room room, List<Piece> pieces, BaseboardOptions o, double baseZ)
        {
            int created = 0;
            double half = o.Thickness / 2;
            Level level = room.Level;
            if (level == null) return 0;
            int count = pieces.Count;

            for (int i = 0; i < count; i++)
            {
                Piece p = pieces[i];
                if (!p.Active) continue;

                if (!(p.Curve is Line line))
                {
                    // Trechos curvos: desloca a curva inteira, sem recortes de portas.
                    Curve off = OffsetCurve(p.Curve, p.Inward, half);
                    if (off != null && NewWall(room, off, o, baseZ, false, false)) created++;
                    continue;
                }

                XYZ t = Geo.FlatDir(line.Direction);
                XYZ shift = p.Inward * half;
                Line centre = Line.CreateBound(line.GetEndPoint(0) + shift, line.GetEndPoint(1) + shift);

                // Cantos: prolonga/encurta até encontrar o rodapé do trecho vizinho.
                Piece prev = pieces[(i - 1 + count) % count], next = pieces[(i + 1) % count];
                double start = centre.GetEndPoint(0).DotProduct(t), end = centre.GetEndPoint(1).DotProduct(t);
                bool joinStart = false, joinEnd = false;
                if (prev.Active && prev.Curve is Line pl && Corner(pl, prev.Inward, centre, half, out double ps))
                {
                    start = ps;
                    joinStart = true;
                }
                if (next.Active && next.Curve is Line nl && Corner(nl, next.Inward, centre, half, out double ns))
                {
                    end = ns;
                    joinEnd = true;
                }

                // Remove os vãos das portas.
                var segments = new List<(double a, double b, bool ja, bool jb)> { (start, end, joinStart, joinEnd) };
                foreach ((double a, double b) cut in p.Cuts.OrderBy(c => c.a))
                {
                    var next2 = new List<(double, double, bool, bool)>();
                    foreach ((double a, double b, bool ja, bool jb) s in segments)
                    {
                        double lo = Math.Min(s.a, s.b), hi = Math.Max(s.a, s.b);
                        if (cut.b <= lo || cut.a >= hi)
                        {
                            next2.Add(s);
                            continue;
                        }
                        bool forward = s.a <= s.b;
                        if (forward)
                        {
                            if (cut.a > s.a) next2.Add((s.a, cut.a, s.ja, false));
                            if (cut.b < s.b) next2.Add((cut.b, s.b, false, s.jb));
                        }
                        else
                        {
                            if (cut.b < s.a) next2.Add((s.a, cut.b, s.ja, false));
                            if (cut.a > s.b) next2.Add((cut.a, s.b, false, s.jb));
                        }
                    }
                    segments = next2;
                }

                XYZ o0 = centre.GetEndPoint(0);
                double t0 = o0.DotProduct(t);
                foreach ((double a, double b, bool ja, bool jb) s in segments)
                {
                    if (Math.Abs(s.b - s.a) < Conv.Cm(1)) continue;
                    Line l = Line.CreateBound(o0 + t * (s.a - t0), o0 + t * (s.b - t0));
                    if (NewWall(room, l, o, baseZ, s.ja, s.jb)) created++;
                }
            }
            return created;
        }

        /// <summary>
        /// Interseção da linha central deste rodapé com a do vizinho (ambas deslocadas para dentro
        /// do ambiente). Retorna a posição ao longo da linha atual.
        /// </summary>
        private static bool Corner(Line neighbour, XYZ neighbourInward, Line centre, double half, out double position)
        {
            position = 0;
            XYZ t = Geo.FlatDir(centre.Direction);
            XYZ u = Geo.FlatDir(neighbour.Direction);
            if (Geo.IsParallel(t, u, 0.9995)) return false;
            XYZ q = neighbour.GetEndPoint(0) + neighbourInward * half;
            XYZ p = centre.GetEndPoint(0);
            // p + t·a = q + u·b (em planta)
            double den = t.X * u.Y - t.Y * u.X;
            double a = ((q.X - p.X) * u.Y - (q.Y - p.Y) * u.X) / den;
            XYZ hit = p + t * a;
            // Só aceita cantos próximos da ponta do trecho (evita ligar trechos distantes).
            double dist = Math.Min(hit.DistanceTo(centre.GetEndPoint(0)), hit.DistanceTo(centre.GetEndPoint(1)));
            if (dist > half * 6 + Conv.Cm(2)) return false;
            position = hit.DotProduct(t);
            return true;
        }

        private static Curve OffsetCurve(Curve c, XYZ inward, double d)
        {
            try
            {
                XYZ mid = c.Evaluate(0.5, true);
                Curve a = c.CreateOffset(d, XYZ.BasisZ);
                if ((a.Evaluate(0.5, true) - mid).DotProduct(inward) > 0) return a;
                return c.CreateOffset(-d, XYZ.BasisZ);
            }
            catch
            {
                return null;
            }
        }

        private bool NewWall(Room room, Curve curve, BaseboardOptions o, double baseZ, bool joinStart, bool joinEnd)
        {
            Curve flat = Flatten(curve, room.Level.Elevation);
            if (flat == null) return false;
            Wall w = null;
            bool ok = Tx.TrySub(_doc, () =>
            {
                w = Wall.Create(_doc, flat, o.WallTypeId, room.LevelId, o.Height, baseZ - room.Level.Elevation, false, false);
                Q.Set(w, BuiltInParameter.WALL_ATTR_ROOM_BOUNDING, 0);
                Q.Set(w, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, Comment(room));
                if (!joinStart) WallUtils.DisallowWallJoinAtEnd(w, 0);
                if (!joinEnd) WallUtils.DisallowWallJoinAtEnd(w, 1);
                Recentre(w, flat);
            });
            if (!ok)
            {
                _report.Warn($"Ambiente {RoomGeo.Label(room)}: um trecho de rodapé não pôde ser criado.");
                return false;
            }
            Length += flat.Length;
            return true;
        }

        /// <summary>Garante que a linha central da parede criada coincida com a curva desejada.</summary>
        private void Recentre(Wall w, Curve wanted)
        {
            int key = w.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM)?.AsInteger() ?? 0;
            if (key == 0 || !(wanted is Line wl)) return;
            _doc.Regenerate();
            Line actual = Q.WallCenterline(w);
            if (actual == null) return;
            XYZ n = Geo.LeftNormal(wl.Direction);
            double delta = (Geo.Mid(wl) - Geo.Mid(actual)).DotProduct(n);
            if (Math.Abs(delta) > Conv.Mm(0.1)) ElementTransformUtils.MoveElement(_doc, w.Id, n * delta);
        }

        private static Curve Flatten(Curve c, double z)
        {
            try
            {
                if (c is Line l) return Line.CreateBound(Geo.WithZ(l.GetEndPoint(0), z), Geo.WithZ(l.GetEndPoint(1), z));
                return c.CreateTransformed(Transform.CreateTranslation(new XYZ(0, 0, z - c.GetEndPoint(0).Z)));
            }
            catch
            {
                return null;
            }
        }

        // ================================================================== perfil de parede

        private int Sweeps(Room room, List<Piece> pieces, BaseboardOptions o, double baseZ)
        {
            int created = 0;
            foreach (Piece p in pieces)
            {
                if (!(p.Host is Wall wall) || !p.Active) continue;
                XYZ mid = p.Curve.Evaluate(0.5, true) + p.Inward * Conv.Cm(3);
                Line cl = Q.WallCenterline(wall);
                XYZ reference = cl != null ? Geo.Mid(cl) : Geo.Mid(p.Curve);
                WallSide side = (mid - reference).DotProduct(wall.Orientation) > 0 ? WallSide.Exterior : WallSide.Interior;
                string key = wall.Id + "|" + side;
                if (!_sweepDone.Add(key)) continue;
                if (!WallSweep.WallAllowsWallSweep(wall))
                {
                    _report.Warn($"A parede {wall.Id} não aceita perfis (ex.: parede cortina).");
                    continue;
                }
                if (HasSweep(wall, o.SweepTypeId, mid)) continue;

                WallSweep ws = TrySweep(wall, side, o, baseZ, mid) ?? TrySweep(wall, Opposite(side), o, baseZ, mid);
                if (ws == null)
                {
                    _report.Warn($"Ambiente {RoomGeo.Label(room)}: o perfil não pôde ser posicionado na face da parede {wall.Id}. Use o método \"Parede de rodapé\".");
                    continue;
                }
                created++;
                Length += p.Curve.Length;
            }
            return created;
        }

        private static WallSide Opposite(WallSide s) => s == WallSide.Exterior ? WallSide.Interior : WallSide.Exterior;

        /// <summary>Cria o perfil e confere se ficou do lado do ambiente; se não, desfaz.</summary>
        private WallSweep TrySweep(Wall wall, WallSide side, BaseboardOptions o, double baseZ, XYZ roomSide)
        {
            WallSweep result = null;
            using (var st = new SubTransaction(_doc))
            {
                st.Start();
                try
                {
                    var info = new WallSweepInfo(WallSweepType.Sweep, false)
                    {
                        WallSide = side,
                        DistanceMeasuredFrom = DistanceMeasuredFrom.Base,
                        Distance = Math.Max(0, baseZ - WallBase(wall)),
                        IsCutByInserts = true,
                    };
                    WallSweep ws = WallSweep.Create(wall, o.SweepTypeId, info);
                    if (ws != null)
                    {
                        Level level = _doc.GetElement(ws.get_Parameter(BuiltInParameter.WALL_SWEEP_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId) as Level;
                        if (level != null) Q.Set(ws, BuiltInParameter.WALL_SWEEP_OFFSET_PARAM, baseZ - level.Elevation);
                        Q.Set(ws, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, Marker);
                        _doc.Regenerate();
                        if (OnSide(ws, wall, roomSide)) result = ws;
                    }
                }
                catch
                {
                    result = null;
                }
                if (result != null) st.Commit();
                else st.RollBack();
            }
            return result;
        }

        private static bool OnSide(WallSweep ws, Wall wall, XYZ roomSide)
        {
            BoundingBoxXYZ bb = ws.get_BoundingBox(null);
            Line cl = Q.WallCenterline(wall);
            if (bb == null || cl == null) return true;
            XYZ n = Geo.LeftNormal(cl.Direction);
            XYZ c = (bb.Min + bb.Max) / 2;
            double sweepSide = (c - Geo.Mid(cl)).DotProduct(n);
            double roomSideSign = (roomSide - Geo.Mid(cl)).DotProduct(n);
            // Perfis longos em paredes inclinadas têm caixa grande: só recusa quando claramente do outro lado.
            return Math.Sign(sweepSide) == Math.Sign(roomSideSign) || Math.Abs(sweepSide) < Conv.Mm(1);
        }

        private bool HasSweep(Wall wall, ElementId typeId, XYZ roomSide)
        {
            foreach (ElementId id in wall.GetDependentElements(new ElementClassFilter(typeof(WallSweep))))
            {
                if (_doc.GetElement(id) is WallSweep ws && ws.GetTypeId() == typeId && OnSide(ws, wall, roomSide))
                {
                    _report.Count("faces que já tinham o perfil (ignoradas)");
                    return true;
                }
            }
            return false;
        }

        private double WallBase(Wall w)
        {
            Level l = _doc.GetElement(w.LevelId) as Level;
            return (l?.Elevation ?? 0) + (Q.Double(w, BuiltInParameter.WALL_BASE_OFFSET) ?? 0);
        }

        // ================================================================== tipos

        /// <summary>Tipo de parede de rodapé (camada única de acabamento), criado se não existir.</summary>
        public static WallType EnsureWallType(Document doc, double thickness, Material material)
        {
            string name = $"DetalhaBIM - Rodapé {Conv.Format(Conv.ToCm(thickness), 1)} cm" + (material != null ? " - " + material.Name : string.Empty);
            name = ViewTools.Sanitize(name);
            WallType existing = Q.All<WallType>(doc).FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            WallType basic = Q.All<WallType>(doc).FirstOrDefault(t => t.Kind == WallKind.Basic)
                             ?? throw new UserMessageException("O projeto não possui nenhum tipo de parede básica para servir de base ao rodapé.");
            var wt = (WallType)basic.Duplicate(name);
            ElementId mat = material?.Id ?? ElementId.InvalidElementId;
            foreach (MaterialFunctionAssignment fn in new[] { MaterialFunctionAssignment.Finish1, MaterialFunctionAssignment.Structure })
            {
                try
                {
                    wt.SetCompoundStructure(CompoundStructure.CreateSingleLayerCompoundStructure(fn, thickness, mat));
                    break;
                }
                catch
                {
                    // Tenta a próxima função de camada.
                }
            }
            Q.Set(wt, BuiltInParameter.FUNCTION_PARAM, 0);
            return wt;
        }
    }
}
