using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class JoineryOptions
    {
        public bool Plan { get; set; } = true;
        public bool Front { get; set; } = true;
        public bool Side { get; set; } = true;
        public bool Isometric { get; set; } = true;
        public bool Dimension { get; set; } = true;
        /// <summary>A vista frontal olha para a face "Frente" da família (padrão do Revit); falso = lado oposto.</summary>
        public bool FamilyFront { get; set; } = true;
        public bool HideMarkers { get; set; } = true;
        public int Scale { get; set; } = 20;
        public int IsoScale { get; set; } = 20;
        public string PlanTemplate { get; set; }
        public string ElevationTemplate { get; set; }
        public string IsoTemplate { get; set; }
        public double Margin { get; set; }
        public DimensionType DimensionType { get; set; }
        public double DimensionOffset { get; set; } = 8;
    }

    /// <summary>
    /// Detalhamento de marcenaria e mobiliário: para cada peça, planta recortada, vista frontal,
    /// vista lateral e isométrico com caixa de corte — todos cotados e nomeados.
    /// </summary>
    public class JoineryDetailService
    {
        private readonly Document _doc;
        private readonly ViewTools _vt;
        private readonly Report _report;
        private readonly ElementId _planType, _elevType, _3dType;

        public JoineryDetailService(Document doc, ViewTools vt, Report report)
        {
            _doc = doc;
            _vt = vt;
            _report = report;
            _planType = vt.FamilyType(ViewFamily.FloorPlan)?.Id;
            _elevType = vt.FamilyType(ViewFamily.Elevation)?.Id;
            _3dType = vt.FamilyType(ViewFamily.ThreeDimensional)?.Id;
        }

        /// <summary>Nome da peça: Marca de tipo, Marca ou Família - Tipo.</summary>
        public static string PieceName(Element e)
        {
            Element type = e.Document.GetElement(e.GetTypeId());
            string tm = Q.Text(type, BuiltInParameter.ALL_MODEL_TYPE_MARK);
            if (!string.IsNullOrWhiteSpace(tm)) return tm;
            string mark = Q.Text(e, BuiltInParameter.ALL_MODEL_MARK);
            string fam = e is FamilyInstance fi ? fi.Symbol?.FamilyName + " - " + fi.Name : e.Name;
            return string.IsNullOrWhiteSpace(mark) ? fam : mark + " - " + fam;
        }

        /// <summary>Cria as vistas da peça. Deve ser chamado dentro de uma transação.</summary>
        public List<View> Create(Element e, string name, JoineryOptions o, View active)
        {
            var views = new List<View>();
            ObjectDimensionService.Axes(e, active, out XYZ x, out XYZ y);
            ElementScan scan = ElementScan.Of(e, null);
            if (scan.Points.Count == 0)
            {
                _report.Warn($"{name}: peça sem geometria.");
                return views;
            }
            scan.Extent(x, out double x0, out double x1);
            scan.Extent(y, out double y0, out double y1);
            scan.Extent(XYZ.BasisZ, out double z0, out double z1);
            XYZ center = x * ((x0 + x1) / 2) + y * ((y0 + y1) / 2) + XYZ.BasisZ * ((z0 + z1) / 2);
            var corners = new List<XYZ>();
            foreach (double a in new[] { x0, x1 })
            foreach (double b in new[] { y0, y1 })
            foreach (double c in new[] { z0, z1 })
                corners.Add(x * a + y * b + XYZ.BasisZ * c);

            // Frente: face "Frente" da família fica no lado -Y (convenção dos modelos de família do Revit).
            XYZ front = y.Negate();
            if (e is FamilyInstance fi && !Geo.FlatDir(fi.FacingOrientation).IsZeroLength()) front = Geo.FlatDir(fi.FacingOrientation).Negate();
            if (!o.FamilyFront) front = front.Negate();
            XYZ side = XYZ.BasisZ.CrossProduct(front).Normalize(); // lado direito de quem olha a frente

            Level level = Q.Level(_doc, e) ?? active?.GenLevel ?? Q.Levels(_doc).FirstOrDefault();
            if (level == null) throw new UserMessageException("O projeto não possui níveis.");

            ViewPlan plan = null;
            if (o.Plan && _planType != null)
            {
                plan = ViewPlan.Create(_doc, _planType, level.Id);
                _vt.SetName(plan, name + " - PLANTA");
                ViewTools.SetScale(plan, o.Scale);
                _vt.ApplyTemplate(plan, o.PlanTemplate, _report);
                ViewTools.CropToPoints(plan, corners, o.Margin);
                views.Add(plan);
                _report.Count("plantas de marcenaria");
            }

            if ((o.Front || o.Side) && _elevType != null)
            {
                ViewPlan host = plan ?? _vt.FloorPlanOf(level.Id, active);
                if (host == null)
                {
                    host = ViewPlan.Create(_doc, _planType, level.Id);
                    _vt.SetName(host, level.Name);
                }
                if (o.Front) Add(views, Elevation(host, front, center, corners, name + " - VISTA FRONTAL", o));
                if (o.Side) Add(views, Elevation(host, side, center, corners, name + " - VISTA LATERAL", o));
            }

            View3D iso = null;
            if (o.Isometric && _3dType != null)
            {
                iso = Isometric(x, y, front, side, x0, x1, y0, y1, z0, z1, center, name + " - ISOMÉTRICO", o);
                views.Add(iso);
                _report.Count("isométricos de marcenaria");
            }

            if (o.Dimension)
            {
                _doc.Regenerate();
                foreach (View v in views) Dimension(e, v, o);
            }
            return views;
        }

        private static void Add(List<View> views, View v)
        {
            if (v != null) views.Add(v);
        }

        private ViewSection Elevation(ViewPlan host, XYZ towardViewer, XYZ center, List<XYZ> corners, string name, JoineryOptions o)
        {
            try
            {
                double depth = corners.Max(p => (p - center).DotProduct(towardViewer));
                double back = corners.Max(p => (center - p).DotProduct(towardViewer));
                double dist = Conv.Cm(60);
                XYZ marker = center + towardViewer * (depth + dist);
                ViewSection v = ElevationFactory.Create(_doc, _elevType, host, marker, towardViewer, o.Scale);
                _vt.SetName(v, name);
                ViewTools.SetScale(v, o.Scale);
                _vt.ApplyTemplate(v, o.ElevationTemplate, _report);
                ViewTools.CropToPoints(v, corners, o.Margin);
                ElevationFactory.SetFarClip(v, dist + depth + back + Conv.Cm(10));
                if (o.HideMarkers) ElevationFactory.HideMarkerInPlans(v);
                _report.Count("vistas de marcenaria");
                return v;
            }
            catch (Exception ex)
            {
                _report.Warn($"{name}: falha ao criar a vista ({ex.Message}).");
                return null;
            }
        }

        private View3D Isometric(XYZ x, XYZ y, XYZ front, XYZ side, double x0, double x1, double y0, double y1, double z0, double z1, XYZ center, string name, JoineryOptions o)
        {
            View3D v = View3D.CreateIsometric(_doc, _3dType);
            _vt.SetName(v, name);
            ViewTools.SetScale(v, o.IsoScale);
            _vt.ApplyTemplate(v, o.IsoTemplate, _report);

            double m = Conv.Cm(3);
            Transform tr = Transform.Identity;
            tr.BasisX = x;
            tr.BasisY = y;
            tr.BasisZ = XYZ.BasisZ;
            v.SetSectionBox(new BoundingBoxXYZ
            {
                Transform = tr,
                Min = new XYZ(x0 - m, y0 - m, z0 - m),
                Max = new XYZ(x1 + m, y1 + m, z1 + m),
            });

            XYZ toEye = (front + side * 0.75 + XYZ.BasisZ * 0.7).Normalize();
            XYZ forward = toEye.Negate();
            XYZ right = forward.CrossProduct(XYZ.BasisZ).Normalize();
            XYZ up = right.CrossProduct(forward).Normalize();
            v.SetOrientation(new ViewOrientation3D(center + toEye * Conv.M(20), up, forward));

            var cat = new ElementId(BuiltInCategory.OST_SectionBox);
            try
            {
                if (v.CanCategoryBeHidden(cat)) v.SetCategoryHidden(cat, true);
            }
            catch
            {
                // O modelo de vista pode controlar a visibilidade.
            }
            try
            {
                v.SaveOrientationAndLock();
            }
            catch
            {
                _report.Warn($"{name}: não foi possível travar a vista — as cotas 3D foram omitidas.");
            }
            return v;
        }

        private void Dimension(Element e, View v, JoineryOptions o)
        {
            if (v is View3D v3 && !v3.IsLocked) return;
            var builder = new DimensionBuilder(_doc, v, o.DimensionType, Conv.Mm(5));
            var svc = new ObjectDimensionService(_doc, v, builder, _report);
            int n = svc.Dimension(e, new ObjectDimensionOptions
            {
                Width = true,
                Depth = true,
                Height = true,
                WallOpenings = false,
                Offset = Conv.PaperMm(o.DimensionOffset, v),
                AboveRight = v is ViewSection,
            });
            _report.Count("cotas nas vistas de marcenaria", n);
        }
    }
}
