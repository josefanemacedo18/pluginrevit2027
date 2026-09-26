using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class LocationOptions
    {
        /// <summary>Cota até as paredes dos dois lados (senão, só até a mais próxima).</summary>
        public bool BothSides { get; set; } = true;
        /// <summary>Inclui a dimensão do próprio objeto na cadeia.</summary>
        public bool IncludeSize { get; set; } = true;
        /// <summary>Loca pelo eixo (centro) do objeto — ex.: pontos elétricos e hidráulicos.</summary>
        public bool Center { get; set; }
        public bool AxisX { get; set; } = true;
        public bool AxisY { get; set; } = true;
        /// <summary>Em cortes/elevações: altura em relação ao piso acabado.</summary>
        public bool Heights { get; set; } = true;
        public double Offset { get; set; }
    }

    /// <summary>
    /// Locação de mobiliário, marcenaria e pontos (tomadas, interruptores, louças...): cotas do
    /// objeto até as paredes mais próximas em planta e, em cortes/elevações, também a altura em
    /// relação ao piso acabado.
    /// </summary>
    public class LocationDimensionService
    {
        private readonly Document _doc;
        private readonly View _view;
        private readonly DimensionBuilder _builder;
        private readonly Report _report;
        private readonly FaceFinder _faces;
        private readonly List<Element> _obstacles;
        private readonly RefLines _lines;
        private List<Element> _floors;

        public LocationDimensionService(Document doc, View view, DimensionBuilder builder, Report report)
        {
            _doc = doc;
            _view = view;
            _builder = builder;
            _report = report;
            _faces = new FaceFinder(view);
            _obstacles = Q.WallsInView(doc, view).Cast<Element>()
                .Concat(Q.InView(doc, view, BuiltInCategory.OST_Columns))
                .Concat(Q.InView(doc, view, BuiltInCategory.OST_StructuralColumns)).ToList();
            _lines = new RefLines(doc, view);
        }

        public static bool Supports(View v) => v != null && !v.IsTemplate && (v is ViewPlan || v is ViewSection);

        public int Locate(Element e, LocationOptions o)
        {
            string name = ObjectDimensionService.Name(e);
            ObjectDimensionService.Axes(e, _view, out XYZ x, out XYZ y);
            ElementScan scan = ElementScan.Of(e, _view);
            if (scan.Points.Count == 0)
            {
                _report.Warn($"{name}: sem geometria visível nesta vista.");
                return 0;
            }
            XYZ center = scan.Center(x, y, XYZ.BasisZ);
            int n = 0;

            if (_view is ViewPlan)
            {
                XYZ c = Geo.WithZ(center, _view.GenLevel?.Elevation ?? center.Z);
                if (o.AxisX) n += Horizontal(e, scan, x, c, o, name);
                if (o.AxisY) n += Horizontal(e, scan, y, c, o, name);
                return n;
            }

            XYZ right = Geo.FlatDir(_view.RightDirection);
            XYZ axis = Geo.IsParallel(x, right, 0.999) ? x : Geo.IsParallel(y, right, 0.999) ? y : null;
            if (axis == null) _report.Warn($"{name}: o objeto não está paralelo a esta vista — locação horizontal ignorada.");
            else n += Horizontal(e, scan, axis, center, o, name);
            if (o.Heights) n += Vertical(e, scan, center, o, name);
            return n;
        }

        /// <summary>Cadeia ao longo de <paramref name="a"/>: parede — objeto — parede.</summary>
        private int Horizontal(Element e, ElementScan scan, XYZ a, XYZ c, LocationOptions o, string name)
        {
            scan.Extent(a, out double lo, out double hi);
            double reach = Conv.M(25);
            List<RefPos> walls = _faces.Crossing(_obstacles.Where(w => w.Id != e.Id), c - a * reach, c + a * reach, Conv.Mm(1), 1);
            RefPos left = walls.Where(r => r.Position <= lo + Conv.Mm(1)).OrderByDescending(r => r.Position).FirstOrDefault();
            RefPos right = walls.Where(r => r.Position >= hi - Conv.Mm(1)).OrderBy(r => r.Position).FirstOrDefault();
            if (left == null && right == null)
            {
                _report.Warn($"{name}: nenhuma parede encontrada ao longo de um dos eixos.");
                return 0;
            }
            if (!o.BothSides && left != null && right != null)
            {
                if (lo - left.Position <= right.Position - hi) right = null;
                else left = null;
            }

            XYZ through = c + a.CrossProduct(_view.ViewDirection).Normalize() * o.Offset;
            if (Try(HorizontalCandidates(scan, a, lo, hi, left, right, through, o), a, through)) return 1;
            _report.Warn($"{name}: não foi possível criar a locação ao longo de um dos eixos.");
            return 0;
        }

        private IEnumerable<IList<RefPos>> HorizontalCandidates(ElementScan scan, XYZ a, double lo, double hi, RefPos left, RefPos right, XYZ through, LocationOptions o)
        {
            if (o.Center)
            {
                RefPos mid = scan.CenterRef(a);
                if (mid != null) yield return Chain(left, right, new[] { mid });
            }
            foreach (IList<RefPos> pair in scan.ExtremePairs(a))
            {
                RefPos pLo = pair.OrderBy(r => r.Position).First(), pHi = pair.OrderBy(r => r.Position).Last();
                yield return Chain(left, right, ObjectRefs(pLo, pHi, left, right, o));
            }
            // Última alternativa: linhas de referência invisíveis nas extremidades (ou no centro) do objeto.
            if (o.Center)
            {
                double m = (lo + hi) / 2;
                RefPos h = Helper(through + a * (m - through.DotProduct(a)), a, m);
                if (h != null) yield return Chain(left, right, new[] { h });
            }
            else
            {
                RefPos h0 = Helper(through + a * (lo - through.DotProduct(a)), a, lo);
                RefPos h1 = Helper(through + a * (hi - through.DotProduct(a)), a, hi);
                if (h0 != null && h1 != null) yield return Chain(left, right, ObjectRefs(h0, h1, left, right, o));
            }
        }

        private static List<RefPos> ObjectRefs(RefPos pLo, RefPos pHi, RefPos left, RefPos right, LocationOptions o)
        {
            var obj = new List<RefPos>();
            if (o.IncludeSize || left != null) obj.Add(pLo);
            if (o.IncludeSize || right != null) obj.Add(pHi);
            return obj;
        }

        private RefPos Helper(XYZ p, XYZ dir, double pos) => _lines.Across(p, dir, pos, 6);

        /// <summary>Cria a primeira cadeia válida e apaga as linhas auxiliares que não foram usadas.</summary>
        private bool Try(IEnumerable<IList<RefPos>> candidates, XYZ dir, XYZ through)
        {
            _lines.Keep();
            int before = _lines.Created;
            Dimension d = _builder.CreateFirst(candidates, dir, through);
            bool helpers = false;
            if (d != null && _lines.Created > before)
            {
                foreach (Reference r in d.References)
                {
                    if (_doc.GetElement(r.ElementId) is CurveElement) helpers = true;
                }
            }
            _lines.DeleteUnused(d == null ? null : new[] { d });
            if (helpers) _report.Count("locações por linhas auxiliares (família sem referências cotáveis)");
            return d != null;
        }

        private static IList<RefPos> Chain(RefPos left, RefPos right, IEnumerable<RefPos> obj)
        {
            var list = new List<RefPos>();
            if (left != null) list.Add(left);
            list.AddRange(obj);
            if (right != null) list.Add(right);
            return list;
        }

        /// <summary>Cadeia vertical: piso acabado (ou nível) — base (e topo) do objeto.</summary>
        private int Vertical(Element e, ElementScan scan, XYZ c, LocationOptions o, string name)
        {
            scan.Extent(XYZ.BasisZ, out double z0, out double z1);
            RefPos floor = FloorRef(e, c, z0);
            if (floor == null)
            {
                _report.Warn($"{name}: piso ou nível de referência não encontrado para a altura.");
                return 0;
            }
            XYZ right = _view.RightDirection.Normalize();
            scan.Extent(right, out double r0, out double r1);
            XYZ through = c + right * (r1 - c.DotProduct(right) + o.Offset);
            if (Try(VerticalCandidates(scan, floor, z0, z1, through, o), XYZ.BasisZ, through)) return 1;
            _report.Warn($"{name}: não foi possível cotar a altura.");
            return 0;
        }

        private IEnumerable<IList<RefPos>> VerticalCandidates(ElementScan scan, RefPos floor, double z0, double z1, XYZ through, LocationOptions o)
        {
            if (o.Center)
            {
                RefPos mid = scan.CenterRef(XYZ.BasisZ);
                if (mid != null) yield return new List<RefPos> { floor, mid };
            }
            foreach (IList<RefPos> pair in scan.ExtremePairs(XYZ.BasisZ))
            {
                RefPos bottom = pair.OrderBy(r => r.Position).First(), top = pair.OrderBy(r => r.Position).Last();
                yield return o.IncludeSize ? new List<RefPos> { floor, bottom, top } : new List<RefPos> { floor, bottom };
            }
            XYZ z = XYZ.BasisZ;
            if (o.Center)
            {
                double m = (z0 + z1) / 2;
                RefPos h = Helper(through + z * (m - through.Z), z, m);
                if (h != null) yield return new List<RefPos> { floor, h };
            }
            else
            {
                RefPos hb = Helper(through + z * (z0 - through.Z), z, z0);
                RefPos ht = o.IncludeSize ? Helper(through + z * (z1 - through.Z), z, z1) : null;
                if (hb != null) yield return ht != null ? new List<RefPos> { floor, hb, ht } : new List<RefPos> { floor, hb };
            }
        }

        /// <summary>Face superior do piso sob o objeto; se não houver, o nível do objeto.</summary>
        private RefPos FloorRef(Element e, XYZ c, double objectBottom)
        {
            _floors ??= Q.InView(_doc, _view, BuiltInCategory.OST_Floors);
            ScanFace best = null;
            foreach (Element fl in _floors)
            {
                ScanFace f = ElementScan.Of(fl, _view).TopFaceAt(c, objectBottom + Conv.Mm(1));
                if (f != null && (best == null || f.Origin.Z > best.Origin.Z)) best = f;
            }
            if (best != null) return new RefPos(best.Reference, best.Origin.Z, null, 1);

            Level level = Q.Level(_doc, e);
            if (level == null) return null;
            try
            {
                Reference r = level.GetPlaneReference();
                return r == null ? null : new RefPos(r, level.Elevation, level, 1);
            }
            catch
            {
                return null;
            }
        }
    }
}
