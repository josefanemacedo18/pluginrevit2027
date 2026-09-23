using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class FacadeOptions
    {
        public bool Openings { get; set; } = true;
        public bool Walls { get; set; } = true;
        public bool Total { get; set; } = true;
        /// <summary>Distância da 1ª linha até a face externa (pés).</summary>
        public double FirstOffset { get; set; }
        /// <summary>Espaçamento entre linhas (pés).</summary>
        public double Spacing { get; set; }
        /// <summary>Lados (Cotas Externas): inferior, superior, esquerdo, direito.</summary>
        public bool Bottom { get; set; } = true;
        public bool Top { get; set; } = true;
        public bool Left { get; set; } = true;
        public bool Right { get; set; } = true;
    }

    /// <summary>
    /// Cotas de fachada no padrão de prancha brasileiro, com até três linhas paralelas:
    /// 1) vãos (cantos + ombreiras de portas e janelas); 2) paredes internas que chegam na
    /// fachada; 3) cota total.
    /// </summary>
    public class FacadeDimensionService
    {
        private readonly Document _doc;
        private readonly View _view;
        private readonly FaceFinder _faces;
        private readonly DimensionBuilder _builder;
        private readonly Report _report;
        private readonly List<Wall> _walls;
        private readonly List<Element> _columns;
        private readonly double _z;
        private readonly double _maxWidth;
        private readonly Dictionary<ElementId, Line> _centerlines = new Dictionary<ElementId, Line>();

        public FacadeDimensionService(Document doc, View view, DimensionBuilder builder, Report report)
        {
            _doc = doc;
            _view = view;
            _builder = builder;
            _report = report;
            _faces = new FaceFinder(view);
            _walls = Q.WallsInView(doc, view).Where(w => ((LocationCurve)w.Location).Curve is Line).ToList();
            foreach (Wall w in _walls) _centerlines[w.Id] = Q.WallCenterline(w);
            _columns = Q.InView(doc, view, BuiltInCategory.OST_Columns)
                .Concat(Q.InView(doc, view, BuiltInCategory.OST_StructuralColumns)).ToList();
            _z = view.GenLevel?.Elevation ?? 0;
            _maxWidth = _walls.Count > 0 ? _walls.Max(w => w.Width) : Conv.Cm(30);
        }

        public List<Wall> Walls => _walls;

        /// <summary>Linha central real da parede (independente da "Linha de localização").</summary>
        private Line Loc(Wall w)
        {
            if (!_centerlines.TryGetValue(w.Id, out Line l))
            {
                l = Q.WallCenterline(w);
                _centerlines[w.Id] = l;
            }
            return l;
        }

        /// <summary>Posição da face externa da parede ao longo de nOut.</summary>
        private double ExteriorPos(Wall w, XYZ nOut) => Geo.Mid(Loc(w)).DotProduct(nOut) + w.Width / 2;

        // ================================================================== parede escolhida

        /// <summary>Cotas de uma fachada a partir de uma parede clicada e do lado indicado.</summary>
        public int DimensionFromWall(Wall wall, XYZ sidePoint, FacadeOptions o)
        {
            Line line = Q.WallCenterline(wall);
            if (line == null)
            {
                _report.Warn("Paredes curvas não são suportadas por esta ferramenta.");
                return 0;
            }
            _centerlines[wall.Id] = line;
            XYZ t = Geo.FlatDir(line.Direction);
            XYZ n = Geo.LeftNormal(t);
            XYZ nOut = (sidePoint - line.GetEndPoint(0)).DotProduct(n) >= 0 ? n : n.Negate();

            List<Wall> group = AlignedGroup(wall, t, nOut);
            var chains = BuildChains(group, t, nOut);
            double outer = group.Max(w => ExteriorPos(w, nOut));
            return Place(chains, t, nOut, outer, o);
        }

        // ================================================================== todos os lados

        /// <summary>Cotas externas automáticas nos lados escolhidos de toda a edificação da vista.</summary>
        public int DimensionAllSides(FacadeOptions o)
        {
            if (_walls.Count == 0)
            {
                _report.Warn($"Nenhuma parede reta encontrada na vista {_view.Name}.");
                return 0;
            }
            PlanFrame f = PlanFrame.FromWalls(_walls);
            int created = 0;
            var sides = new List<(bool enabled, XYZ nOut, XYZ t, string name)>
            {
                (o.Bottom, f.Y.Negate(), f.X, "inferior"),
                (o.Top, f.Y, f.X, "superior"),
                (o.Left, f.X.Negate(), f.Y, "esquerdo"),
                (o.Right, f.X, f.Y, "direito"),
            };

            foreach ((bool enabled, XYZ nOut, XYZ t, string name) in sides)
            {
                if (!enabled) continue;
                List<Wall> facade = FacadeWalls(t, nOut);
                if (facade.Count == 0)
                {
                    _report.Warn($"Lado {name}: nenhuma parede de fachada identificada.");
                    continue;
                }

                var openings = new List<RefPos>();
                var walls = new List<RefPos>();
                foreach (List<Wall> group in Groups(facade, t, nOut))
                {
                    (List<RefPos> a, List<RefPos> b) = BuildChains(group, t, nOut);
                    openings.AddRange(a);
                    walls.AddRange(b);
                }
                double outer = facade.Max(w => ExteriorPos(w, nOut));
                created += Place((openings, walls), t, nOut, outer, o);
            }
            return created;
        }

        /// <summary>
        /// Identifica as paredes de fachada de um lado por varredura: para cada posição ao longo
        /// do lado, a primeira parede atingida vindo de fora é fachada.
        /// </summary>
        private List<Wall> FacadeWalls(XYZ t, XYZ nOut)
        {
            var rects = new List<(Wall w, double t0, double t1, double outer, bool parallel)>();
            foreach (Wall w in _walls)
            {
                Line l = Loc(w);
                XYZ d = Geo.FlatDir(l.Direction);
                bool parallel = Geo.IsParallel(d, t, 0.9999);
                bool perpendicular = Geo.IsPerpendicular(d, t, 0.01);
                if (!parallel && !perpendicular) continue;
                double a = l.GetEndPoint(0).DotProduct(t), b = l.GetEndPoint(1).DotProduct(t);
                double t0 = Math.Min(a, b), t1 = Math.Max(a, b);
                double outer;
                if (parallel)
                {
                    outer = ExteriorPos(w, nOut);
                }
                else
                {
                    outer = Math.Max(l.GetEndPoint(0).DotProduct(nOut), l.GetEndPoint(1).DotProduct(nOut));
                    double c = Geo.Mid(l).DotProduct(t);
                    t0 = c - w.Width / 2;
                    t1 = c + w.Width / 2;
                }
                rects.Add((w, t0, t1, outer, parallel));
            }
            if (rects.Count == 0) return new List<Wall>();

            double min = rects.Min(r => r.t0), max = rects.Max(r => r.t1);
            double step = Conv.Cm(5);
            var hits = new Dictionary<ElementId, int>();
            for (double s = min + step / 2; s < max; s += step)
            {
                var first = rects.Where(r => s >= r.t0 && s <= r.t1).OrderByDescending(r => r.outer).FirstOrDefault();
                if (first.w == null || !first.parallel) continue;
                hits[first.w.Id] = hits.TryGetValue(first.w.Id, out int c) ? c + 1 : 1;
            }
            return _walls.Where(w => hits.TryGetValue(w.Id, out int c) && c >= 2).ToList();
        }

        /// <summary>Paredes paralelas com a face externa alinhada e contíguas à parede-semente.</summary>
        private List<Wall> AlignedGroup(Wall seed, XYZ t, XYZ nOut)
        {
            double c0 = ExteriorPos(seed, nOut);
            var candidates = _walls.Where(w => Geo.IsParallel(Loc(w).Direction, t, 0.9999)
                                               && Math.Abs(ExteriorPos(w, nOut) - c0) < Conv.Cm(2)).ToList();
            if (!candidates.Any(w => w.Id == seed.Id)) candidates.Add(seed);
            return Groups(candidates, t, nOut).First(g => g.Any(w => w.Id == seed.Id));
        }

        /// <summary>Agrupa paredes alinhadas (mesma face externa) e contíguas.</summary>
        private List<List<Wall>> Groups(List<Wall> walls, XYZ t, XYZ nOut)
        {
            // Agrupa por face externa: posições consecutivas a menos de 2 cm ficam juntas.
            var faces = new List<List<Wall>>();
            double last = double.MinValue;
            foreach (Wall w in walls.OrderBy(w => ExteriorPos(w, nOut)))
            {
                double pos = ExteriorPos(w, nOut);
                if (faces.Count == 0 || pos - last > Conv.Cm(2)) faces.Add(new List<Wall>());
                faces[faces.Count - 1].Add(w);
                last = pos;
            }

            var groups = new List<List<Wall>>();
            foreach (List<Wall> byFace in faces)
            {
                var ordered = byFace.Select(w =>
                {
                    Line l = Loc(w);
                    double a = l.GetEndPoint(0).DotProduct(t), b = l.GetEndPoint(1).DotProduct(t);
                    return (w, t0: Math.Min(a, b), t1: Math.Max(a, b));
                }).OrderBy(x => x.t0).ToList();

                List<Wall> current = null;
                double end = double.MinValue;
                foreach (var x in ordered)
                {
                    if (current == null || x.t0 > end + _maxWidth + Conv.Cm(5))
                    {
                        current = new List<Wall>();
                        groups.Add(current);
                        end = double.MinValue;
                    }
                    current.Add(x.w);
                    end = Math.Max(end, x.t1);
                }
            }
            return groups;
        }

        /// <summary>Monta as referências das cadeias de vãos e de paredes de um grupo alinhado.</summary>
        private (List<RefPos> openings, List<RefPos> walls) BuildChains(List<Wall> group, XYZ t, XYZ nOut)
        {
            double outer = group.Max(w => ExteriorPos(w, nOut));
            double minWidth = group.Min(w => w.Width);
            double maxWidth = group.Max(w => w.Width);
            double tMin = double.MaxValue, tMax = double.MinValue;
            foreach (Wall w in group)
            {
                Line l = Loc(w);
                foreach (XYZ p in new[] { l.GetEndPoint(0), l.GetEndPoint(1) })
                {
                    tMin = Math.Min(tMin, p.DotProduct(t));
                    tMax = Math.Max(tMax, p.DotProduct(t));
                }
            }
            double ext = _maxWidth / 2 + Conv.Cm(3);
            tMin -= ext;
            tMax += ext;

            // Linha 1 — dentro da espessura da parede de fachada.
            XYZ a = Point(t, nOut, tMin, outer - minWidth / 2);
            XYZ b = Point(t, nOut, tMax, outer - minWidth / 2);
            List<Element> candidates = Near(a, b, Conv.M(1));
            List<RefPos> crossing = _faces.Crossing(candidates, a, b, Conv.Mm(1), 2);

            var openings = new List<RefPos>();
            if (crossing.Count > 0)
            {
                openings.Add(crossing.OrderBy(r => r.Position).First());
                openings.Add(crossing.OrderBy(r => r.Position).Last());
                var ids = new HashSet<ElementId>(group.Select(w => w.Id));
                List<RefPos> jambs = crossing.Where(r => ids.Contains(r.Element.Id)).ToList();
                openings.AddRange(RoomDimensionService.OpeningEdges(Q.OpeningsIn(_doc, ids), jambs, t));
            }

            // Linha 2 — logo após a face interna, captando as paredes que chegam na fachada.
            var walls = new List<RefPos>();
            if (openings.Count >= 2)
            {
                double eMin = openings.Min(r => r.Position), eMax = openings.Max(r => r.Position);
                double inner = outer - maxWidth - Conv.Cm(5);
                XYZ c = Point(t, nOut, eMin + Conv.Mm(5), inner);
                XYZ d = Point(t, nOut, eMax - Conv.Mm(5), inner);
                walls.AddRange(_faces.Crossing(Near(c, d, Conv.M(1)), c, d, Conv.Mm(1), 2));
                walls.Add(openings.OrderBy(r => r.Position).First());
                walls.Add(openings.OrderBy(r => r.Position).Last());
            }
            return (openings, walls);
        }

        private int Place((List<RefPos> openings, List<RefPos> walls) chains, XYZ t, XYZ nOut, double outer, FacadeOptions o)
        {
            List<RefPos> a = _builder.Dedupe(chains.openings);
            List<RefPos> b = _builder.Dedupe(chains.walls);
            List<RefPos> total = _builder.Extremes(chains.openings.Concat(chains.walls));

            // Não repete linhas idênticas (ex.: fachada sem janelas = cota total).
            bool drawA = o.Openings && a.Count >= 2 && !(o.Total && Same(a, total));
            bool drawB = o.Walls && b.Count >= 2 && !(drawA && Same(a, b)) && !(o.Total && Same(b, total));
            bool drawT = o.Total && total.Count == 2;

            int created = 0, line = 0;
            XYZ Through(int k) => Point(t, nOut, 0, outer + o.FirstOffset + k * o.Spacing);

            if (drawA && _builder.Create(a, t, Through(line++)) != null) created++;
            if (drawB && _builder.Create(b, t, Through(line++)) != null) created++;
            if (drawT && _builder.Create(total, t, Through(line)) != null) created++;
            return created;
        }

        private static bool Same(List<RefPos> x, List<RefPos> y)
        {
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++)
            {
                if (Math.Abs(x[i].Position - y[i].Position) > Conv.Mm(1)) return false;
            }
            return true;
        }

        private XYZ Point(XYZ t, XYZ nOut, double along, double across)
        {
            return new XYZ(0, 0, _z) + t * along + nOut * across;
        }

        /// <summary>Paredes e pilares cuja caixa envolvente se aproxima do segmento A→B.</summary>
        private List<Element> Near(XYZ a, XYZ b, double margin)
        {
            double minX = Math.Min(a.X, b.X) - margin, maxX = Math.Max(a.X, b.X) + margin;
            double minY = Math.Min(a.Y, b.Y) - margin, maxY = Math.Max(a.Y, b.Y) + margin;
            return _walls.Cast<Element>().Concat(_columns).Where(e =>
            {
                BoundingBoxXYZ bb = e.get_BoundingBox(null);
                return bb != null && bb.Max.X >= minX && bb.Min.X <= maxX && bb.Max.Y >= minY && bb.Min.Y <= maxY;
            }).ToList();
        }
    }
}
