using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class AlignedOptions
    {
        /// <summary>1ª linha: extremidades + ombreiras de portas e janelas.</summary>
        public bool Openings { get; set; } = true;
        /// <summary>2ª linha: paredes que chegam na face cotada.</summary>
        public bool Walls { get; set; } = true;
        /// <summary>3ª linha: comprimento total da face.</summary>
        public bool Total { get; set; } = true;
        /// <summary>Cota a espessura da parede no ponto clicado.</summary>
        public bool Thickness { get; set; }
        public double FirstOffset { get; set; }
        public double Spacing { get; set; }
    }

    /// <summary>
    /// Cotas alinhadas à parede, em qualquer ângulo. A face cotada é a do lado clicado; nas
    /// pontas chanfradas (encontro com paredes inclinadas) o Revit não oferece face paralela,
    /// então são usadas linhas de detalhe invisíveis exatamente nos cantos da face.
    /// </summary>
    public class AlignedDimensionService
    {
        private readonly Document _doc;
        private readonly View _view;
        private readonly DimensionBuilder _builder;
        private readonly Report _report;
        private readonly FaceFinder _faces;
        private readonly RefLines _lines;
        private readonly List<Element> _obstacles;
        private readonly double _z;

        public AlignedDimensionService(Document doc, ViewPlan view, DimensionBuilder builder, Report report)
        {
            _doc = doc;
            _view = view;
            _builder = builder;
            _report = report;
            _faces = new FaceFinder(view);
            _lines = new RefLines(doc, view);
            _obstacles = Q.WallsInView(doc, view).Cast<Element>()
                .Concat(Q.InView(doc, view, BuiltInCategory.OST_Columns))
                .Concat(Q.InView(doc, view, BuiltInCategory.OST_StructuralColumns)).ToList();
            _z = view.GenLevel?.Elevation ?? 0;
        }

        /// <summary>Cotas da face da parede voltada para <paramref name="sidePoint"/>.</summary>
        public int FromWall(Wall wall, XYZ sidePoint, AlignedOptions o)
        {
            Line cl = Q.WallCenterline(wall);
            if (cl == null)
            {
                _report.Warn("Paredes curvas não são suportadas pela cota alinhada.");
                return 0;
            }
            XYZ t = Geo.FlatDir(cl.Direction);
            XYZ n = Geo.LeftNormal(t);
            XYZ c0 = Geo.WithZ(cl.GetEndPoint(0), _z);
            XYZ nOut = (sidePoint - c0).DotProduct(n) >= 0 ? n : n.Negate();

            // Face da parede do lado clicado e sua extensão ao longo da parede.
            List<FaceInfo> sideFaces = _faces.FacesNormalTo(wall, n);
            double facePos = sideFaces.Count > 0 ? sideFaces.Max(f => f.Origin.DotProduct(nOut)) : c0.DotProduct(nOut) + wall.Width / 2;
            List<FaceInfo> face = sideFaces.Where(f => Math.Abs(f.Origin.DotProduct(nOut) - facePos) < Conv.Mm(1)).ToList();
            double e0, e1;
            if (face.Count > 0)
            {
                e0 = face.Min(f => Range(f, t).min);
                e1 = face.Max(f => Range(f, t).max);
            }
            else
            {
                e0 = Math.Min(cl.GetEndPoint(0).DotProduct(t), cl.GetEndPoint(1).DotProduct(t));
                e1 = Math.Max(cl.GetEndPoint(0).DotProduct(t), cl.GetEndPoint(1).DotProduct(t));
            }
            if (e1 - e0 < Conv.Cm(1)) return 0;

            // Ponto sobre o plano da face, na posição "pos" ao longo da parede.
            XYZ P(double pos, double outward = 0) =>
                c0 + nOut * (facePos - c0.DotProduct(nOut) + outward) + t * (pos - c0.DotProduct(t));

            // Faces perpendiculares à parede cruzadas logo por dentro da face: pontas e ombreiras.
            List<RefPos> inner = _faces.Crossing(_obstacles, P(e0 - Conv.Cm(2), -Conv.Cm(1)), P(e1 + Conv.Cm(2), -Conv.Cm(1)), Conv.Mm(1), 2);
            RefPos End(double pos)
            {
                RefPos face1 = inner.Where(r => Math.Abs(r.Position - pos) < Conv.Mm(2)).OrderBy(r => Math.Abs(r.Position - pos)).FirstOrDefault();
                if (face1 != null) return new RefPos(face1.Reference, face1.Position, face1.Element, 1);
                return _lines.Across(P(pos), t, pos, 1);
            }
            RefPos a = End(e0), b = End(e1);
            if (a == null || b == null)
            {
                _report.Warn("Não foi possível obter as extremidades da face da parede.");
                return 0;
            }
            var ends = new List<RefPos> { a, b };

            var chains = new List<List<RefPos>>();
            if (o.Openings)
            {
                List<FamilyInstance> openings = Q.OpeningsIn(_doc, new[] { wall.Id }).Where(fi =>
                {
                    double p = Geo.ElementPoint(fi).DotProduct(t);
                    return p > e0 && p < e1;
                }).ToList();
                if (openings.Count > 0)
                {
                    List<RefPos> jambs = inner.Where(r => r.Element.Id == wall.Id && r.Position > e0 + Conv.Cm(1) && r.Position < e1 - Conv.Cm(1)).ToList();
                    chains.Add(ends.Concat(RoomDimensionService.OpeningEdges(openings, jambs, t)).ToList());
                }
            }
            if (o.Walls)
            {
                List<RefPos> meeting = _faces.Crossing(_obstacles.Where(e => e.Id != wall.Id), P(e0 - Conv.Cm(2), Conv.Cm(2)), P(e1 + Conv.Cm(2), Conv.Cm(2)), Conv.Mm(1), 2)
                    .Where(r => r.Position > e0 + Conv.Cm(1) && r.Position < e1 - Conv.Cm(1)).ToList();
                if (meeting.Count > 0) chains.Add(ends.Concat(meeting).ToList());
            }
            if (o.Total || chains.Count == 0) chains.Add(ends);

            int created = 0;
            double offset = o.FirstOffset;
            foreach (List<RefPos> chain in chains)
            {
                if (_builder.Create(chain, t, P((e0 + e1) / 2, offset)) != null)
                {
                    created++;
                    offset += o.Spacing;
                }
            }

            if (o.Thickness)
            {
                List<RefPos> sides = _faces.FacesNormalTo(wall, n).Select(f => new RefPos(f.Reference, f.Origin.DotProduct(n), wall, 1)).ToList();
                if (sides.Count >= 2)
                {
                    double pos = Math.Max(e0 + Conv.Cm(5), Math.Min(e1 - Conv.Cm(5), sidePoint.DotProduct(t)));
                    if (_builder.Create(_builder.Extremes(sides), n, P(pos)) != null) created++;
                }
            }
            return created;
        }

        /// <summary>Cota alinhada à direção informada entre dois pontos quaisquer.</summary>
        public bool BetweenPoints(XYZ dir, XYZ p1, XYZ p2, XYZ through)
        {
            XYZ t = Geo.FlatDir(dir);
            if (t.IsZeroLength()) return false;
            p1 = Geo.WithZ(p1, _z);
            p2 = Geo.WithZ(p2, _z);
            if (Math.Abs((p2 - p1).DotProduct(t)) < Conv.Mm(2))
            {
                _report.Warn("Os dois pontos estão na mesma posição ao longo da direção escolhida.");
                return false;
            }
            RefPos a = _lines.Across(p1, t, p1.DotProduct(t), 1);
            RefPos b = _lines.Across(p2, t, p2.DotProduct(t), 1);
            if (a == null || b == null) return false;
            return _builder.Create(new[] { a, b }, t, Geo.WithZ(through, _z)) != null;
        }

        /// <summary>Extensão da face ao longo de t (a direção H da face pode ter qualquer sentido).</summary>
        private static (double min, double max) Range(FaceInfo f, XYZ t)
        {
            double s = f.H.DotProduct(t) >= 0 ? 1 : -1;
            double a = f.HMin * s, b = f.HMax * s;
            return (Math.Min(a, b), Math.Max(a, b));
        }

        /// <summary>Direção de um elemento linear (parede, eixo, plano de referência, linha) ou de uma família.</summary>
        public static XYZ DirectionOf(Element e)
        {
            switch (e)
            {
                case Wall w when w.Location is LocationCurve lc && lc.Curve is Line l:
                    return Geo.FlatDir(l.Direction);
                case Grid g when g.Curve is Line gl:
                    return Geo.FlatDir(gl.Direction);
                case ReferencePlane rp:
                    return Geo.FlatDir(rp.FreeEnd - rp.BubbleEnd);
                case CurveElement ce when ce.GeometryCurve is Line cl:
                    return Geo.FlatDir(cl.Direction);
                case FamilyInstance fi:
                    return Geo.FlatDir(fi.HandOrientation);
            }
            return XYZ.Zero;
        }
    }
}
