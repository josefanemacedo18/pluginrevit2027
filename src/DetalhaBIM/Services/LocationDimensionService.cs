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

            var candidates = new List<IList<RefPos>>();
            if (o.Center)
            {
                RefPos mid = scan.CenterRef(a);
                if (mid != null) candidates.Add(Chain(left, right, new[] { mid }));
            }
            foreach (IList<RefPos> pair in scan.ExtremePairs(a))
            {
                RefPos pLo = pair.OrderBy(r => r.Position).First(), pHi = pair.OrderBy(r => r.Position).Last();
                var obj = new List<RefPos>();
                if (o.IncludeSize || left != null) obj.Add(pLo);
                if (o.IncludeSize || right != null) obj.Add(pHi);
                candidates.Add(Chain(left, right, obj));
            }
            if (candidates.Count == 0)
            {
                _report.Warn($"{name}: o objeto não tem faces ou planos de referência nesta direção.");
                return 0;
            }
            XYZ through = c + a.CrossProduct(_view.ViewDirection).Normalize() * o.Offset;
            if (_builder.CreateFirst(candidates, a, through) != null) return 1;
            _report.Warn($"{name}: o Revit não aceitou as referências da locação.");
            return 0;
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
            var candidates = new List<IList<RefPos>>();
            if (o.Center)
            {
                RefPos mid = scan.CenterRef(XYZ.BasisZ);
                if (mid != null) candidates.Add(new List<RefPos> { floor, mid });
            }
            foreach (IList<RefPos> pair in scan.ExtremePairs(XYZ.BasisZ))
            {
                RefPos bottom = pair.OrderBy(r => r.Position).First(), top = pair.OrderBy(r => r.Position).Last();
                candidates.Add(o.IncludeSize ? new List<RefPos> { floor, bottom, top } : new List<RefPos> { floor, bottom });
            }
            if (candidates.Count == 0) return 0;

            XYZ right = _view.RightDirection.Normalize();
            scan.Extent(right, out double r0, out double r1);
            XYZ through = c + right * (r1 - c.DotProduct(right) + o.Offset);
            if (_builder.CreateFirst(candidates, XYZ.BasisZ, through) != null) return 1;
            _report.Warn($"{name}: o Revit não aceitou as referências da altura.");
            return 0;
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
