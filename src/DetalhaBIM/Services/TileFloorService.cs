using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class TileFloorOptions
    {
        /// <summary>0 = um piso dividido em peças (Parts) nas juntas; 1 = um piso (Floor) por placa.</summary>
        public int Method { get; set; }
        public FloorType Type { get; set; }
        /// <summary>Dimensões da placa e da junta (pés).</summary>
        public double Width { get; set; }
        public double Length { get; set; }
        public double Joint { get; set; }
        /// <summary>0 = junta centralizada, 1 = placa centralizada, 2 = canto, 3 = ponto clicado.</summary>
        public int Start { get; set; }
        public XYZ Point { get; set; }
        /// <summary>Ângulo em relação à maior parede do ambiente (graus).</summary>
        public double AngleDeg { get; set; }
        /// <summary>Apoia a paginação sobre o piso (laje/contrapiso) existente no ambiente.</summary>
        public bool OnExisting { get; set; } = true;
        /// <summary>Deslocamento adicional da base (pés).</summary>
        public double Offset { get; set; }
        public bool Replace { get; set; } = true;
        public bool Note { get; set; }
        public string Description { get; set; }
        public double Loss { get; set; }
        public double BoxM2 { get; set; }
    }

    /// <summary>
    /// Paginação de piso modelada: cria o piso de revestimento do ambiente com o tipo/material da
    /// placa e o divide nas juntas em peças reais (Parts), com a junta como vão entre as placas —
    /// ou cria um piso por placa. As placas cortadas seguem o contorno do ambiente e contornam pilares.
    /// </summary>
    public class TileFloorService
    {
        public const string Marker = "PAGINAÇÃO";
        public const string OldGroupPrefix = "Paginação - ";
        /// <summary>Acima disto a divisão fica pesada: o piso é criado sem dividir (use padrão de superfície).</summary>
        public const int MaxParts = 6000;
        public const int MaxFloors = 2500;

        private readonly Document _doc;
        private readonly ViewPlan _view;
        private readonly Report _report;
        private readonly double _tol;
        private List<Floor> _floors;

        /// <summary>Verdadeiro se algum piso foi dividido em peças (só então a visibilidade de peças importa).</summary>
        public bool PartsCreated { get; private set; }

        public TileFloorService(Document doc, ViewPlan view, Report report, double shortCurveTolerance)
        {
            _doc = doc;
            _view = view;
            _report = report;
            _tol = shortCurveTolerance;
        }

        public static readonly string[] Headers =
        {
            "Ambiente", "Área (m²)", "Placa (cm)", "Junta (mm)", "Inteiras", "Cortadas", "Total de placas",
            "Área + perda (m²)", "Caixas", "Modelado como",
        };

        public static string Comment(Room room) => Marker + " - " + RoomGeo.Label(room);

        /// <summary>Espessura do tipo de piso (pés).</summary>
        public static double Thickness(FloorType t)
        {
            try
            {
                double w = t.GetCompoundStructure()?.GetWidth() ?? 0;
                if (w > Geo.Eps) return w;
            }
            catch
            {
                // tipo sem estrutura
            }
            return Q.Double(t, BuiltInParameter.FLOOR_ATTR_DEFAULT_THICKNESS_PARAM) ?? Conv.Cm(1);
        }

        /// <summary>Cria a paginação de um ambiente e devolve a linha do quantitativo (dentro de uma transação).</summary>
        public string[] Layout(Room room, TileFloorOptions o)
        {
            string label = RoomGeo.Label(room);
            PlanFrame frame = RoomGeo.Frame(room);
            List<List<P2>> loops2D = Loops2D(room, frame);
            List<CurveLoop> loops = RoomGeo.Loops(room, _tol);
            if (loops2D.Count == 0 || loops.Count == 0)
            {
                _report.Warn($"Ambiente {label}: contorno inválido.");
                return null;
            }

            P2 x = P2.FromAngle(o.AngleDeg * Math.PI / 180);
            P2 start = StartPoint(loops2D, frame, o);
            P2 origin = Plano2D.GridOrigin(o.Start >= 2 ? 2 : o.Start, start, x, o.Width, o.Length, o.Joint);
            List<Tile> tiles = Plano2D.Tiles(loops2D, origin, x, o.Width, o.Length, o.Joint);
            var count = new TileCount
            {
                Inteiras = tiles.Count(t => t.Whole),
                Cortadas = tiles.Count(t => !t.Whole),
                AreaRegiao = Plano2D.NetArea(loops2D),
                AreaPeca = o.Width * o.Length,
            };

            string comment = Comment(room);
            if (o.Replace) RemovePrevious(room, comment);

            double thickness = Thickness(o.Type);
            _floors ??= Q.All<Floor>(_doc);
            double baseZ = (o.OnExisting ? RoomGeo.FloorTop(_doc, room, f => !f.IsValidObject || IsOurs(f), _floors) : null) ?? RoomGeo.BaseElevation(room);
            double top = baseZ + o.Offset + thickness;

            int method = o.Method;
            if (method == 1 && tiles.Count > MaxFloors)
            {
                _report.Warn($"Ambiente {label}: {tiles.Count} placas é demais para um piso por placa — usado o piso dividido em peças.");
                method = 0;
            }

            string how;
            if (method == 0)
            {
                how = PartsFloor(room, loops, frame, origin, x, o, top, comment, tiles.Count, loops2D);
                if (how == null && tiles.Count <= MaxFloors)
                {
                    _report.Warn($"Ambiente {label}: a divisão em peças não foi aceita pelo Revit — criado um piso por placa.");
                    how = TileFloors(room, loops, frame, origin, x, tiles, o, top, comment);
                }
            }
            else
            {
                how = TileFloors(room, loops, frame, origin, x, tiles, o, top, comment);
            }
            if (how == null)
            {
                _report.Warn($"Ambiente {label}: o piso paginado não pôde ser criado.");
                return null;
            }

            if (o.Note) AddNote(room, frame, count, o);
            _report.Count("ambientes paginados");
            return Row(room, count, o, how);
        }

        private static bool IsOurs(Floor f) => Q.Text(f, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).StartsWith(Marker + " - ", StringComparison.Ordinal);

        // ================================================================== piso dividido em peças

        private string PartsFloor(Room room, List<CurveLoop> loops, PlanFrame frame, P2 origin, P2 x, TileFloorOptions o, double top, string comment, int tileCount, List<List<P2>> loops2D)
        {
            Floor floor = CreateFloor(room, loops, o, top, comment);
            if (floor == null) return null;
            if (tileCount > MaxParts)
            {
                _report.Warn($"Ambiente {RoomGeo.Label(room)}: {tileCount} placas — o piso foi criado sem divisão (placa pequena demais). Use um material com padrão de superfície.");
                return "piso sem divisão";
            }

            bool divided = Tx.TrySub(_doc, () =>
            {
                _doc.Regenerate();
                var ids = new List<ElementId> { floor.Id };
                if (!PartUtils.AreElementsValidForCreateParts(_doc, ids)) throw new InvalidOperationException("Piso não aceita peças.");
                PartUtils.CreateParts(_doc, ids);
                _doc.Regenerate();
                ICollection<ElementId> parts = PartUtils.GetAssociatedParts(_doc, floor.Id, false, true);
                if (parts.Count == 0) throw new InvalidOperationException("Peças não criadas.");

                List<Curve> curves = DivisionCurves(loops2D, frame, origin, x, o, top);
                if (curves.Count == 0) return; // ambiente menor que uma placa: uma peça só
                SketchPlane sp = SketchPlane.Create(_doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, top)));
                PartMaker maker = PartUtils.DivideParts(_doc, parts, new List<ElementId>(), curves, sp.Id);
                if (maker == null) throw new InvalidOperationException("Divisão não criada.");
                PartUtils.GetPartMakerMethodToDivideVolumeFW(maker).DivisionGap = Math.Max(0, o.Joint);
                _doc.Regenerate();
            });
            if (divided)
            {
                PartsCreated = true;
                _report.Count("pisos paginados (divididos em placas)");
                return "piso dividido em peças";
            }
            try
            {
                _doc.Delete(floor.Id);
            }
            catch
            {
                // mantém
            }
            return null;
        }

        /// <summary>Linhas de junta (eixo) atravessando todo o ambiente, no plano do topo do piso.</summary>
        private static List<Curve> DivisionCurves(List<List<P2>> loops2D, PlanFrame frame, P2 origin, P2 x, TileFloorOptions o, double z)
        {
            var curves = new List<Curve>();
            (List<double> xs, List<double> ys) = Plano2D.JointPositions(loops2D, origin, x, o.Width, o.Length, o.Joint);
            List<List<P2>> grid = Plano2D.ToGrid(loops2D, origin, x);
            double minX = grid.SelectMany(l => l).Min(p => p.X) - Conv.Cm(20), maxX = grid.SelectMany(l => l).Max(p => p.X) + Conv.Cm(20);
            double minY = grid.SelectMany(l => l).Min(p => p.Y) - Conv.Cm(20), maxY = grid.SelectMany(l => l).Max(p => p.Y) + Conv.Cm(20);
            XYZ W(double gx, double gy)
            {
                P2 l = Plano2D.FromGrid(new P2(gx, gy), origin, x);
                return frame.World(l.X, l.Y, z);
            }
            foreach (double gx in xs) curves.Add(Line.CreateBound(W(gx, minY), W(gx, maxY)));
            foreach (double gy in ys) curves.Add(Line.CreateBound(W(minX, gy), W(maxX, gy)));
            return curves;
        }

        // ================================================================== um piso por placa

        private string TileFloors(Room room, List<CurveLoop> loops, PlanFrame frame, P2 origin, P2 x, List<Tile> tiles, TileFloorOptions o, double top, string comment)
        {
            double z0 = loops[0].First().GetEndPoint(0).Z;
            Solid region = null;
            try
            {
                region = GeometryCreationUtilities.CreateExtrusionGeometry(loops, XYZ.BasisZ, Conv.Cm(10));
            }
            catch
            {
                _report.Warn($"Ambiente {RoomGeo.Label(room)}: contorno não aceito para recortar as placas — só as inteiras foram criadas.");
            }

            int created = 0, failed = 0;
            foreach (Tile t in tiles)
            {
                XYZ W(double gx, double gy)
                {
                    P2 l = Plano2D.FromGrid(new P2(gx, gy), origin, x);
                    return frame.World(l.X, l.Y, z0);
                }
                CurveLoop rect;
                try
                {
                    XYZ a = W(t.X0, t.Y0), b = W(t.X1, t.Y0), c = W(t.X1, t.Y1), d = W(t.X0, t.Y1);
                    rect = CurveLoop.Create(new List<Curve> { Line.CreateBound(a, b), Line.CreateBound(b, c), Line.CreateBound(c, d), Line.CreateBound(d, a) });
                }
                catch
                {
                    failed++;
                    continue;
                }

                List<List<CurveLoop>> pieces = t.Whole ? new List<List<CurveLoop>> { new List<CurveLoop> { rect } } : Cut(region, rect);
                foreach (List<CurveLoop> profile in pieces)
                {
                    if (CreateFloor(room, profile, o, top, comment, false) != null) created++;
                    else failed++;
                }
            }
            if (failed > 0) _report.Warn($"Ambiente {RoomGeo.Label(room)}: {failed} placa(s) não puderam ser criadas (recortes muito pequenos).");
            if (created == 0) return null;
            _report.Count("placas modeladas (um piso por placa)", created);
            return "um piso por placa";
        }

        /// <summary>Recorte da placa pelo ambiente (interseção de sólidos): um ou mais perfis (com furos).</summary>
        private static List<List<CurveLoop>> Cut(Solid region, CurveLoop rect)
        {
            var result = new List<List<CurveLoop>>();
            if (region == null) return result;
            try
            {
                Solid tile = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { rect }, XYZ.BasisZ, Conv.Cm(10));
                Solid inter = BooleanOperationsUtils.ExecuteBooleanOperation(region, tile, BooleanOperationsType.Intersect);
                if (inter == null || inter.Volume < Geo.Eps) return result;
                foreach (Face f in inter.Faces)
                {
                    if (!(f is PlanarFace pf) || pf.FaceNormal.Z > -0.99) continue;
                    List<CurveLoop> profile = pf.GetEdgesAsCurveLoops().OrderByDescending(Geo.LoopArea).ToList();
                    if (profile.Count > 0 && Geo.LoopArea(profile[0]) > Conv.Cm(1) * Conv.Cm(1)) result.Add(profile);
                }
            }
            catch
            {
                // recorte impossível
            }
            return result;
        }

        // ================================================================== comum

        private Floor CreateFloor(Room room, IList<CurveLoop> profile, TileFloorOptions o, double top, string comment, bool warn = true)
        {
            Floor floor = null;
            bool ok = Tx.TrySub(_doc, () =>
            {
                floor = Floor.Create(_doc, profile, o.Type.Id, room.LevelId);
                Level level = _doc.GetElement(room.LevelId) as Level;
                Q.Set(floor, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM, top - (level?.Elevation ?? 0));
                Q.Set(floor, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, comment);
            });
            if (!ok && warn) _report.Warn($"Ambiente {RoomGeo.Label(room)}: o Revit não aceitou o contorno do piso.");
            return ok ? floor : null;
        }

        /// <summary>Remove a paginação anterior do ambiente: pisos do DetalhaBIM e as linhas da versão 1.3.</summary>
        private void RemovePrevious(Room room, string comment)
        {
            List<ElementId> floors = Q.All<Floor>(_doc).Where(f => Q.Text(f, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) == comment)
                .Select(f => f.Id).ToList();
            if (floors.Count > 0 && Tx.TrySub(_doc, () => _doc.Delete(floors)))
                _report.Count("paginações anteriores substituídas");

            string old = OldGroupPrefix + RoomGeo.Label(room);
            foreach (Group g in Q.All<Group>(_doc).Where(g => g.GroupType != null
                         && (g.GroupType.Name == old || g.GroupType.Name.StartsWith(old + " (", StringComparison.Ordinal))).ToList())
            {
                Tx.TrySub(_doc, () =>
                {
                    ElementId typeId = g.GetTypeId();
                    _doc.Delete(g.UngroupMembers().ToList());
                    if (_doc.GetElement(typeId) is GroupType gt && gt.Groups.IsEmpty) _doc.Delete(typeId);
                });
                _report.Count("desenhos de paginação antigos (linhas) removidos");
            }
        }

        private void AddNote(Room room, PlanFrame frame, TileCount c, TileFloorOptions o)
        {
            try
            {
                string piece = $"{Conv.Format(Conv.ToCm(o.Width), 1)}x{Conv.Format(Conv.ToCm(o.Length), 1)}";
                string head = string.IsNullOrWhiteSpace(o.Description) ? piece : $"{o.Description.Trim()} {piece}";
                string text = $"{head} - {c.Total} pç ({c.Inteiras} inteiras + {c.Cortadas} cortadas)";
                XYZ p = RoomGeo.Point(room);
                XYZ pos = Geo.WithZ(p, _view.GenLevel?.Elevation ?? p.Z) - frame.Y * Conv.PaperMm(9, _view);
                var opts = new TextNoteOptions(_doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType))
                {
                    HorizontalAlignment = HorizontalTextAlignment.Center,
                    Rotation = frame.Angle,
                };
                TextNote.Create(_doc, _view.Id, pos, text, opts);
            }
            catch
            {
                _report.Warn($"Ambiente {RoomGeo.Label(room)}: nota de quantidade não pôde ser criada.");
            }
        }

        private static string[] Row(Room room, TileCount c, TileFloorOptions o, string how)
        {
            double area = Conv.ToM2(c.AreaRegiao);
            double buy = area * (1 + o.Loss);
            string boxes = o.BoxM2 > 0 ? ((int)Math.Ceiling(buy / o.BoxM2 - 1e-9)).ToString() : "-";
            return new[]
            {
                RoomGeo.Label(room),
                Conv.Format(area, 2),
                $"{Conv.Format(Conv.ToCm(o.Width), 1)} x {Conv.Format(Conv.ToCm(o.Length), 1)}",
                Conv.Format(Conv.ToCm(o.Joint) * 10, 1),
                c.Inteiras.ToString(),
                c.Cortadas.ToString(),
                c.Total.ToString(),
                Conv.Format(buy, 2),
                boxes,
                how,
            };
        }

        /// <summary>Laços do contorno de acabamento no sistema local do ambiente (plano 2D).</summary>
        private static List<List<P2>> Loops2D(Room room, PlanFrame frame)
        {
            var result = new List<List<P2>>();
            foreach (IList<BoundarySegment> loop in RoomGeo.Segments(room))
            {
                var pts = new List<P2>();
                foreach (BoundarySegment s in loop)
                {
                    IList<XYZ> t = s.GetCurve().Tessellate();
                    for (int i = 0; i < t.Count - 1; i++) pts.Add(new P2(frame.LX(t[i]), frame.LY(t[i])));
                }
                if (pts.Count >= 3) result.Add(pts);
            }
            return result;
        }

        private static P2 StartPoint(List<List<P2>> loops, PlanFrame frame, TileFloorOptions o)
        {
            double minX = loops.SelectMany(l => l).Min(p => p.X), maxX = loops.SelectMany(l => l).Max(p => p.X);
            double minY = loops.SelectMany(l => l).Min(p => p.Y), maxY = loops.SelectMany(l => l).Max(p => p.Y);
            switch (o.Start)
            {
                case 2:
                    return new P2(minX, minY);
                case 3 when o.Point != null:
                    return new P2(frame.LX(o.Point), frame.LY(o.Point));
                default:
                    return new P2((minX + maxX) / 2, (minY + maxY) / 2);
            }
        }

        // ================================================================== tipos e visibilidade

        /// <summary>Tipo de piso da placa (camada única com o material), criado se não existir.</summary>
        public static FloorType EnsureType(Document doc, string description, double w, double l, double thickness, Material material)
        {
            string desc = string.IsNullOrWhiteSpace(description) ? "Piso" : description.Trim();
            string name = ViewTools.Sanitize($"DetalhaBIM - {desc} {Conv.Format(Conv.ToCm(w), 1)}x{Conv.Format(Conv.ToCm(l), 1)} - {Conv.Format(Conv.ToCm(thickness), 1)} cm"
                                             + (material != null ? " - " + material.Name : string.Empty));
            FloorType existing = Q.All<FloorType>(doc).FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;
            FloorType basis = Q.All<FloorType>(doc).FirstOrDefault(t => !t.IsFoundationSlab)
                              ?? throw new UserMessageException("O projeto não possui nenhum tipo de piso para servir de base.");
            var ft = (FloorType)basis.Duplicate(name);
            ElementId mat = material?.Id ?? ElementId.InvalidElementId;
            foreach (MaterialFunctionAssignment fn in new[] { MaterialFunctionAssignment.Finish1, MaterialFunctionAssignment.Structure })
            {
                try
                {
                    ft.SetCompoundStructure(CompoundStructure.CreateSingleLayerCompoundStructure(fn, thickness, mat));
                    break;
                }
                catch
                {
                    // Tenta a próxima função de camada.
                }
            }
            return ft;
        }

        /// <summary>Mostra as peças (placas) nas vistas informadas. Retorna quantas vistas foram ajustadas.</summary>
        public static int ShowParts(IEnumerable<View> views)
        {
            int n = 0;
            foreach (View v in views)
            {
                try
                {
                    if (v.IsTemplate || v.PartsVisibility == PartsVisibility.ShowPartsOnly || v.PartsVisibility == PartsVisibility.ShowPartsAndOriginal) continue;
                    v.PartsVisibility = PartsVisibility.ShowPartsOnly;
                    n++;
                }
                catch
                {
                    // Controlado pelo modelo de vista.
                }
            }
            return n;
        }
    }
}
