using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class ObjectDimensionOptions
    {
        /// <summary>Largura (eixo X do objeto ou comprimento da parede).</summary>
        public bool Width { get; set; } = true;
        /// <summary>Profundidade (eixo Y do objeto ou espessura da parede).</summary>
        public bool Depth { get; set; } = true;
        public bool Height { get; set; } = true;
        /// <summary>Paredes: cadeia com os vãos de portas e janelas ao longo da parede.</summary>
        public bool WallOpenings { get; set; } = true;
        /// <summary>Distância entre o objeto e a linha de cota (pés).</summary>
        public double Offset { get; set; }
        /// <summary>Plantas e elevações: cotas acima/à direita (senão abaixo/à esquerda).</summary>
        public bool AboveRight { get; set; }
    }

    /// <summary>
    /// Cota as dimensões gerais de paredes e de qualquer família (mobiliário, marcenaria,
    /// louças, luminárias, equipamentos...) em plantas, cortes/elevações e vistas 3D isométricas.
    /// </summary>
    public class ObjectDimensionService
    {
        private readonly Document _doc;
        private readonly View _view;
        private readonly DimensionBuilder _builder;
        private readonly Report _report;
        private readonly RefLines _refLines;

        public ObjectDimensionService(Document doc, View view, DimensionBuilder builder, Report report)
        {
            _doc = doc;
            _view = view;
            _builder = builder;
            _report = report;
            _refLines = RefLines.Supported(view) ? new RefLines(doc, view) : null;
        }

        /// <summary>Vistas em que a ferramenta funciona.</summary>
        public static bool Supports(View v) =>
            v != null && !v.IsTemplate && (v is ViewPlan || v is ViewSection || (v is View3D v3 && !v3.IsPerspective));

        /// <summary>Eixos horizontais do objeto: direção da parede ou eixo X da família.</summary>
        public static void Axes(Element e, View view, out XYZ x, out XYZ y)
        {
            x = XYZ.Zero;
            if (e is Wall w && w.Location is LocationCurve lc && lc.Curve is Line l)
            {
                x = Geo.FlatDir(l.Direction);
            }
            else if (e is FamilyInstance fi)
            {
                try
                {
                    Transform t = fi.GetTransform();
                    x = Geo.FlatDir(t.BasisX);
                    if (x.IsZeroLength()) x = Geo.FlatDir(t.BasisY);
                }
                catch
                {
                    // usa a vista
                }
            }
            if (x.IsZeroLength()) x = Geo.FlatDir(view?.RightDirection ?? XYZ.BasisX);
            if (x.IsZeroLength()) x = XYZ.BasisX;
            y = XYZ.BasisZ.CrossProduct(x).Normalize();
        }

        /// <summary>Cria as cotas do elemento na vista. Retorna quantas foram criadas.</summary>
        public int Dimension(Element e, ObjectDimensionOptions o)
        {
            Axes(e, _view, out XYZ x, out XYZ y);
            XYZ z = XYZ.BasisZ;
            ElementScan scan = ElementScan.Of(e, _view);
            if (scan.Points.Count == 0)
            {
                _report.Warn($"{Name(e)}: elemento sem geometria visível nesta vista.");
                return 0;
            }
            var box = new Box(scan, x, y, z);
            string name = Name(e);

            if (_view is View3D) return In3D(e, scan, box, o, name);
            if (_view is ViewPlan) return InPlan(e, scan, box, o, name);
            return InSection(e, scan, box, o, name);
        }

        // ================================================================== planta

        private int InPlan(Element e, ElementScan scan, Box b, ObjectDimensionOptions o, string name)
        {
            int n = 0;
            double z = _view.GenLevel?.Elevation ?? b.Z0;
            XYZ right = Geo.FlatDir(_view.RightDirection), up = Geo.FlatDir(_view.UpDirection);

            if (o.Width)
            {
                XYZ side = PlanSide(b.Y, up, right, o.AboveRight);
                XYZ through = Geo.WithZ(b.Point(0, side.DotProduct(b.Y) > 0 ? b.Y1 : b.Y0, 0) + side * o.Offset, z);
                n += Try(Candidates(e, scan, b, b.X, null, through), b.X, through, name, e is Wall ? "o comprimento" : "a largura");
                if (e is Wall && o.WallOpenings)
                {
                    XYZ t2 = through + side * Math.Max(o.Offset, Conv.PaperMm(7, _view));
                    n += WallChain(e as Wall, scan, b, t2);
                }
            }
            if (o.Depth)
            {
                XYZ side = PlanSide(b.X, up, right, o.AboveRight);
                XYZ through = Geo.WithZ(b.Point(side.DotProduct(b.X) > 0 ? b.X1 : b.X0, 0, 0) + side * o.Offset, z);
                n += Try(Candidates(e, scan, b, b.Y, null, through), b.Y, through, name, e is Wall ? "a espessura" : "a profundidade");
            }
            return n;
        }

        /// <summary>Lado (±perp) para posicionar a cota: abaixo/à esquerda ou acima/à direita.</summary>
        private static XYZ PlanSide(XYZ perp, XYZ up, XYZ right, bool aboveRight)
        {
            double u = perp.DotProduct(up);
            double s = Math.Abs(u) > 0.2 ? u : perp.DotProduct(right);
            bool positive = s > 0;
            return positive == aboveRight ? perp : perp.Negate();
        }

        // ================================================================== corte / elevação

        private int InSection(Element e, ElementScan scan, Box b, ObjectDimensionOptions o, string name)
        {
            int n = 0;
            XYZ right = _view.RightDirection.Normalize(), up = _view.UpDirection.Normalize();

            if (o.Width)
            {
                XYZ axis = Geo.IsParallel(b.X, right, 0.999) ? b.X : Geo.IsParallel(b.Y, right, 0.999) ? b.Y : null;
                if (axis == null)
                {
                    _report.Warn($"{name}: o objeto não está paralelo a esta vista — a largura não foi cotada.");
                }
                else
                {
                    double zz = o.AboveRight ? b.Z1 + o.Offset : b.Z0 - o.Offset;
                    XYZ through = Geo.WithZ(b.Center, zz);
                    n += Try(Candidates(e, scan, b, axis, null, through), axis, through, name, "a largura");
                }
            }
            if (o.Height && Geo.IsParallel(up, XYZ.BasisZ, 0.999))
            {
                XYZ sideDir = o.AboveRight ? right : right.Negate();
                Extent(b, sideDir, out double lo, out double hi);
                XYZ through = b.Center + sideDir * (hi - b.Center.DotProduct(sideDir) + o.Offset);
                n += Try(Candidates(e, scan, b, XYZ.BasisZ, null, through), XYZ.BasisZ, through, name, "a altura");
            }
            return n;
        }

        // ================================================================== 3D

        private int In3D(Element e, ElementScan scan, Box b, ObjectDimensionOptions o, string name)
        {
            int n = 0;
            XYZ v = _view.ViewDirection.Normalize(); // aponta para o observador
            double sx = b.X.DotProduct(v) >= 0 ? 1 : -1;
            double sy = b.Y.DotProduct(v) >= 0 ? 1 : -1;
            double xFace = sx > 0 ? b.X1 : b.X0, yFace = sy > 0 ? b.Y1 : b.Y0;

            if (o.Width)
            {
                // Sobre o topo do objeto, do lado voltado para o observador.
                XYZ through = b.Point(0, yFace, b.Z1) + b.Y * (sy * o.Offset);
                _builder.PlaneNormal = XYZ.BasisZ;
                n += Try(Candidates(e, scan, b, b.X, XYZ.BasisZ, through), b.X, through, name, e is Wall ? "o comprimento" : "a largura");
                if (e is Wall && o.WallOpenings)
                {
                    XYZ t2 = through + b.Y * (sy * Math.Max(o.Offset, Conv.PaperMm(7, _view)));
                    _builder.PlaneNormal = XYZ.BasisZ;
                    n += WallChain(e as Wall, scan, b, t2);
                }
            }
            if (o.Depth)
            {
                XYZ through = b.Point(xFace, 0, b.Z1) + b.X * (sx * o.Offset);
                _builder.PlaneNormal = XYZ.BasisZ;
                n += Try(Candidates(e, scan, b, b.Y, XYZ.BasisZ, through), b.Y, through, name, e is Wall ? "a espessura" : "a profundidade");
            }
            if (o.Height)
            {
                // Na quina voltada para o observador, no plano da face mais visível.
                bool frontIsY = Math.Abs(b.Y.DotProduct(v)) >= Math.Abs(b.X.DotProduct(v));
                XYZ normal = frontIsY ? b.Y : b.X;
                XYZ offsetDir = frontIsY ? b.X * sx : b.Y * sy;
                XYZ through = b.Point(xFace, yFace, 0) + offsetDir * o.Offset;
                _builder.PlaneNormal = normal;
                n += Try(Candidates(e, scan, b, XYZ.BasisZ, normal, through), XYZ.BasisZ, through, name, "a altura");
            }
            _builder.PlaneNormal = null;
            return n;
        }

        // ================================================================== referências

        /// <summary>
        /// Alternativas de referências para uma dimensão, na ordem: (paredes) faces das pontas;
        /// planos de referência da família; faces; arestas/linhas simbólicas; e, por último, linhas
        /// de referência invisíveis nas extremidades do objeto — criadas só se as anteriores
        /// falharem, para que a cota saia sempre.
        /// </summary>
        private IEnumerable<IList<RefPos>> Candidates(Element e, ElementScan scan, Box b, XYZ axis, XYZ planeNormal, XYZ through)
        {
            double lo = b.Lo(axis), hi = b.Hi(axis);
            bool wallLength = e is Wall && axis.IsAlmostEqualTo(b.X);
            if (wallLength)
            {
                List<RefPos> ends = scan.FacesAlong(axis, 1);
                RefPos a = ends.FirstOrDefault(r => Math.Abs(r.Position - lo) < Conv.Mm(2));
                RefPos c = ends.FirstOrDefault(r => Math.Abs(r.Position - hi) < Conv.Mm(2));
                if (a != null && c != null) yield return new List<RefPos> { a, c };
            }
            foreach (IList<RefPos> pair in scan.ExtremePairs(axis, planeNormal))
            {
                // Paredes: só vale de ponta a ponta (e não entre ombreiras de vãos).
                if (wallLength && (Math.Abs(pair.Min(r => r.Position) - lo) > Conv.Mm(3) || Math.Abs(pair.Max(r => r.Position) - hi) > Conv.Mm(3))) continue;
                yield return pair;
            }
            if (_refLines == null) yield break;
            RefPos h0 = _refLines.Across(through + axis * (lo - through.DotProduct(axis)), axis, lo, 6, planeNormal);
            RefPos h1 = _refLines.Across(through + axis * (hi - through.DotProduct(axis)), axis, hi, 6, planeNormal);
            if (h0 != null && h1 != null) yield return new List<RefPos> { h0, h1 };
        }

        /// <summary>Cadeia ao longo da parede com as extremidades e as ombreiras dos vãos.</summary>
        private int WallChain(Wall wall, ElementScan scan, Box b, XYZ through)
        {
            List<RefPos> faces = scan.FacesAlong(b.X, 2);
            if (faces.Count <= 2) return 0; // sem vãos: seria igual à cota de comprimento
            List<FamilyInstance> openings = Q.OpeningsIn(_doc, new[] { wall.Id });
            if (openings.Count == 0) return 0;
            List<RefPos> refs = RoomDimensionService.OpeningEdges(openings, faces, b.X);
            RefPos lo = faces.OrderBy(r => r.Position).First(), hi = faces.OrderBy(r => r.Position).Last();
            refs.Add(lo);
            refs.Add(hi);
            if (_builder.Create(refs, b.X, through) == null) return 0;
            _report.Count("cotas de vãos em paredes");
            return 1;
        }

        // ================================================================== auxiliares

        private int Try(IEnumerable<IList<RefPos>> candidates, XYZ dir, XYZ through, string name, string what)
        {
            _refLines?.Keep();
            int before = _refLines?.Created ?? 0;
            Dimension d = _builder.CreateFirst(candidates, dir, through);
            bool usedHelpers = d != null && UsesCurveElements(d);
            _refLines?.DeleteUnused(d == null ? null : new[] { d });
            if (d == null)
            {
                _report.Warn($"{name}: não foi possível cotar {what} nesta vista.");
                return 0;
            }
            if (usedHelpers && (_refLines?.Created ?? 0) > before)
                _report.Count("cotas por linhas auxiliares (família sem referências cotáveis — refaça se mover o objeto)");
            return 1;
        }

        private bool UsesCurveElements(Dimension d)
        {
            try
            {
                foreach (Reference r in d.References)
                {
                    if (_doc.GetElement(r.ElementId) is CurveElement) return true;
                }
            }
            catch
            {
                // referências ilegíveis
            }
            return false;
        }

        private static void Extent(Box b, XYZ dir, out double lo, out double hi)
        {
            lo = double.MaxValue;
            hi = double.MinValue;
            foreach (double x in new[] { b.X0, b.X1 })
            foreach (double y in new[] { b.Y0, b.Y1 })
            foreach (double z in new[] { b.Z0, b.Z1 })
            {
                double v = b.Point(x, y, z).DotProduct(dir);
                lo = Math.Min(lo, v);
                hi = Math.Max(hi, v);
            }
        }

        public static string Name(Element e)
        {
            if (e == null) return "Elemento";
            string mark = Q.Text(e, BuiltInParameter.ALL_MODEL_MARK);
            string label = e is FamilyInstance fi ? fi.Symbol?.FamilyName + " : " + fi.Name : e.Category?.Name + " : " + e.Name;
            return string.IsNullOrWhiteSpace(mark) ? $"{label} (id {e.Id})" : $"{label} [{mark}]";
        }

        /// <summary>Caixa envolvente do objeto no seu próprio sistema de eixos (X, Y horizontais e Z).</summary>
        private class Box
        {
            public Box(ElementScan scan, XYZ x, XYZ y, XYZ z)
            {
                X = x;
                Y = y;
                Z = z;
                scan.Extent(x, out double x0, out double x1);
                scan.Extent(y, out double y0, out double y1);
                scan.Extent(z, out double z0, out double z1);
                X0 = x0;
                X1 = x1;
                Y0 = y0;
                Y1 = y1;
                Z0 = z0;
                Z1 = z1;
            }

            public XYZ X { get; }
            public XYZ Y { get; }
            public XYZ Z { get; }
            public double X0 { get; }
            public double X1 { get; }
            public double Y0 { get; }
            public double Y1 { get; }
            public double Z0 { get; }
            public double Z1 { get; }

            /// <summary>Ponto em coordenadas locais (x, y, z já medidos ao longo dos eixos).</summary>
            public XYZ Point(double x, double y, double z) => X * x + Y * y + Z * z;

            public XYZ Center => Point((X0 + X1) / 2, (Y0 + Y1) / 2, (Z0 + Z1) / 2);

            public double Lo(XYZ axis) => axis.IsAlmostEqualTo(X) ? X0 : axis.IsAlmostEqualTo(Y) ? Y0 : Z0;
            public double Hi(XYZ axis) => axis.IsAlmostEqualTo(X) ? X1 : axis.IsAlmostEqualTo(Y) ? Y1 : Z1;
        }
    }
}
