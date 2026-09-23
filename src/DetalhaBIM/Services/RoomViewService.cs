using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class RoomViewOptions
    {
        public bool Plan { get; set; } = true;
        public bool Ceiling { get; set; } = true;
        public bool Elevations { get; set; } = true;
        public bool Isometric { get; set; } = true;

        public string PlanTemplate { get; set; }
        public string CeilingTemplate { get; set; }
        public string ElevationTemplate { get; set; }
        public string IsoTemplate { get; set; }

        public int PlanScale { get; set; } = 25;
        public int ElevationScale { get; set; } = 25;
        public int IsoScale { get; set; } = 50;

        public bool DimensionPlan { get; set; }
        public bool TagPlan { get; set; }
        public bool DimensionElevations { get; set; }
        public bool SkipSeparationLines { get; set; } = true;
    }

    /// <summary>
    /// Gera o conjunto de vistas de detalhamento de um ambiente: planta e forro recortados,
    /// uma elevação interna por parede e um isométrico com caixa de corte.
    /// </summary>
    public class RoomViewService
    {
        private readonly Document _doc;
        private readonly ViewTools _vt;
        private readonly Report _report;
        private readonly VistasSettings _vs;
        private readonly CotasSettings _cs;
        private readonly ElementId _floorType, _ceilingType, _elevType, _3dType;

        public RoomViewService(Document doc, ViewTools vt, Report report, DetalhaSettings settings)
        {
            _doc = doc;
            _vt = vt;
            _report = report;
            _vs = settings.Vistas;
            _cs = settings.Cotas;
            _floorType = vt.FamilyType(ViewFamily.FloorPlan)?.Id;
            _ceilingType = vt.FamilyType(ViewFamily.CeilingPlan)?.Id;
            _elevType = vt.FamilyType(ViewFamily.Elevation)?.Id;
            _3dType = vt.FamilyType(ViewFamily.ThreeDimensional)?.Id;
        }

        private double Margin => Conv.Cm(_vs.FolgaRecorteCm);

        /// <summary>Cria as vistas do ambiente. Deve ser chamado dentro de uma transação.</summary>
        public List<View> Create(Room room, RoomViewOptions o, View activeView)
        {
            var views = new List<View>();
            ViewPlan plan = null;

            if (o.Plan && _floorType != null)
            {
                plan = CreatePlan(room, _floorType, _vs.SufixoPlanta, o.PlanTemplate, o.PlanScale);
                views.Add(plan);
                _report.Count("plantas de ambiente");
            }
            if (o.Ceiling && _ceilingType != null)
            {
                views.Add(CreatePlan(room, _ceilingType, _vs.SufixoForro, o.CeilingTemplate, o.PlanScale));
                _report.Count("plantas de forro de ambiente");
            }
            if (o.Elevations && _elevType != null)
            {
                ViewPlan host = plan ?? _vt.FloorPlanOf(room.LevelId, activeView);
                if (host == null)
                {
                    host = ViewPlan.Create(_doc, _floorType, room.LevelId);
                    _vt.SetName(host, room.Level?.Name ?? "Planta");
                }
                List<ViewSection> elevations = CreateElevations(room, host, o);
                views.AddRange(elevations);
                _report.Count("elevações internas", elevations.Count);
            }
            if (o.Isometric && _3dType != null)
            {
                views.Add(CreateIsometric(room, o.IsoTemplate, o.IsoScale));
                _report.Count("isométricos");
            }

            if (plan != null && (o.DimensionPlan || o.TagPlan))
            {
                _doc.Regenerate();
                if (o.TagPlan)
                {
                    var tags = new TagService(_doc, _report);
                    tags.TagRooms(plan, null, true);
                    tags.TagCategory(plan, BuiltInCategory.OST_Doors, null, true, 4);
                    tags.TagCategory(plan, BuiltInCategory.OST_Windows, null, true, 4);
                }
                if (o.DimensionPlan)
                {
                    var builder = DimensionBuilder.FromSettings(_doc, plan, _cs);
                    var svc = new RoomDimensionService(_doc, plan, builder, _report);
                    int n = svc.Dimension(room, new RoomDimensionOptions
                    {
                        AtCenter = true,
                        Openings = true,
                        OpeningsOffset = Conv.PaperMm(_cs.AfastamentoInternoMm, plan),
                    });
                    _report.Count("cotas nas plantas de ambiente", n);
                }
            }
            return views;
        }

        private ViewPlan CreatePlan(Room room, ElementId typeId, string suffix, string template, int scale)
        {
            ViewPlan v = ViewPlan.Create(_doc, typeId, room.LevelId);
            _vt.SetName(v, RoomGeo.FormatName(room, _vs.PadraoNome, suffix));
            ViewTools.SetScale(v, scale);
            _vt.ApplyTemplate(v, template, _report);
            ViewTools.CropToPoints(v, RoomGeo.BoundaryPoints(room), Margin);
            return v;
        }

        private List<ViewSection> CreateElevations(Room room, ViewPlan host, RoomViewOptions o)
        {
            var result = new List<ViewSection>();
            double baseZ = RoomGeo.BaseElevation(room);
            double topZ = RoomGeo.TopElevation(room);
            double minLen = Conv.Cm(_vs.MenorParedeElevacaoCm);
            double margin = Conv.Cm(_vs.FolgaElevacaoCm);
            List<XYZ> boundary = RoomGeo.BoundaryPoints(room);
            int letter = 0;

            foreach (BoundarySegment seg in RoomGeo.OuterSegments(room))
            {
                if (!(seg.GetCurve() is Line line) || line.Length < minLen) continue;
                Element e = _doc.GetElement(seg.ElementId);
                if (o.SkipSeparationLines && (e == null || e is CurveElement)) continue;

                XYZ t = Geo.FlatDir(line.Direction);
                XYZ n = Geo.LeftNormal(t);
                XYZ mid = Geo.Mid(line);
                if (!RoomGeo.Contains(room, mid + n * Conv.Cm(20))) n = n.Negate();

                // Profundidade disponível do ambiente à frente da parede.
                double depth = boundary.Max(p => (p - mid).DotProduct(n));
                double dist = Math.Max(Conv.Cm(30), Math.Min(Conv.Cm(_vs.DistanciaMarcadorCm), depth * 0.5));
                XYZ markerPoint = mid + n * dist;

                try
                {
                    ViewSection v = ElevationFactory.Create(_doc, _elevType, host, markerPoint, n, o.ElevationScale);
                    string suffix = $"{_vs.SufixoElevacao} {Geo.Letters(letter++)}";
                    _vt.SetName(v, RoomGeo.FormatName(room, _vs.PadraoNome, suffix));
                    ViewTools.SetScale(v, o.ElevationScale);
                    _vt.ApplyTemplate(v, o.ElevationTemplate, _report);

                    var pts = new List<XYZ>
                    {
                        Geo.WithZ(line.GetEndPoint(0), baseZ), Geo.WithZ(line.GetEndPoint(1), baseZ),
                        Geo.WithZ(line.GetEndPoint(0), topZ), Geo.WithZ(line.GetEndPoint(1), topZ),
                    };
                    ViewTools.CropToPoints(v, pts, margin);
                    ElevationFactory.SetFarClip(v, dist + Conv.Cm(30));

                    if (o.DimensionElevations) DimensionElevation(v, line, e, baseZ, topZ);
                    result.Add(v);
                }
                catch (Exception ex)
                {
                    _report.Warn($"Ambiente {RoomGeo.Label(room)}: falha ao criar elevação ({ex.Message}).");
                }
            }
            return result;
        }

        /// <summary>Cota horizontal (largura da parede) e vertical (pé-direito) na elevação interna.</summary>
        private void DimensionElevation(ViewSection v, Line wallLine, Element wall, double baseZ, double topZ)
        {
            // Largura: faces perpendiculares nas extremidades via cruzamento na altura de 1 m.
            var faces = new FaceFinder(v);
            XYZ t = Geo.FlatDir(wallLine.Direction);
            XYZ a = Geo.WithZ(wallLine.GetEndPoint(0), baseZ + Conv.M(1)) - t * Conv.Cm(40);
            XYZ b = Geo.WithZ(wallLine.GetEndPoint(1), baseZ + Conv.M(1)) + t * Conv.Cm(40);
            XYZ n = Geo.LeftNormal(t);
            // Desloca a linha levemente para dentro do ambiente para captar as paredes laterais.
            XYZ into = v.ViewDirection.DotProduct(n) > 0 ? n : n.Negate();
            a += into * Conv.Cm(3);
            b += into * Conv.Cm(3);

            var walls = Q.WallsInView(_doc, v).Cast<Element>().ToList();
            var builder = DimensionBuilder.FromSettings(_doc, v, _cs);
            List<RefPos> crossing = faces.Crossing(walls, a, b, Conv.Mm(1));
            double p0 = wallLine.GetEndPoint(0).DotProduct(t), p1 = wallLine.GetEndPoint(1).DotProduct(t);
            RefPos Closest(double pos) => crossing.Where(r => Math.Abs(r.Position - pos) < Conv.Cm(5))
                .OrderBy(r => Math.Abs(r.Position - pos)).FirstOrDefault();
            var ends = new List<RefPos> { Closest(p0), Closest(p1) }.Where(r => r != null).ToList();
            double off = Conv.PaperMm(_cs.PrimeiraLinhaMm, v);
            if (ends.Count == 2)
            {
                XYZ through = Geo.WithZ(Geo.Mid(wallLine), topZ + off);
                if (builder.Create(ends, t, through) != null) _report.Count("cotas nas elevações");
            }
        }

        private View3D CreateIsometric(Room room, string template, int scale)
        {
            View3D v = View3D.CreateIsometric(_doc, _3dType);
            _vt.SetName(v, RoomGeo.FormatName(room, _vs.PadraoNome, _vs.SufixoIsometrico));
            ViewTools.SetScale(v, scale);
            _vt.ApplyTemplate(v, template, _report);

            PlanFrame f = RoomGeo.Frame(room);
            RoomGeo.Bounds(RoomGeo.BoundaryPoints(room), f, out double minX, out double maxX, out double minY, out double maxY);
            double m = Margin;
            double baseZ = RoomGeo.BaseElevation(room) - Conv.Cm(10);
            double topZ = RoomGeo.TopElevation(room) + Conv.Cm(5);

            Transform tr = Transform.Identity;
            tr.Origin = new XYZ(f.Origin.X, f.Origin.Y, 0);
            tr.BasisX = f.X;
            tr.BasisY = f.Y;
            tr.BasisZ = XYZ.BasisZ;
            var box = new BoundingBoxXYZ
            {
                Transform = tr,
                Min = new XYZ(minX - m, minY - m, baseZ),
                Max = new XYZ(maxX + m, maxY + m, topZ),
            };
            v.SetSectionBox(box);

            // Orientação: SE (padrão), SO, NE ou NO em relação ao eixo do ambiente.
            string ori = (_vs.OrientacaoIsometrico ?? "SE").ToUpperInvariant();
            double sx = ori.Contains("E") ? 1 : -1;
            double sy = ori.StartsWith("N") ? 1 : -1;
            XYZ forward = (f.X * -sx + f.Y * -sy - XYZ.BasisZ * 1.0).Normalize();
            XYZ right = forward.CrossProduct(XYZ.BasisZ).Normalize();
            XYZ up = right.CrossProduct(forward).Normalize();
            XYZ center = f.World((minX + maxX) / 2, (minY + maxY) / 2, (baseZ + topZ) / 2);
            XYZ eye = center - forward * Conv.M(30);
            v.SetOrientation(new ViewOrientation3D(eye, up, forward));

            if (_vs.OcultarCaixaCorte)
            {
                var cat = new ElementId(BuiltInCategory.OST_SectionBox);
                try
                {
                    if (v.CanCategoryBeHidden(cat)) v.SetCategoryHidden(cat, true);
                }
                catch
                {
                    // Modelo de vista pode controlar a visibilidade.
                }
            }
            return v;
        }
    }
}
