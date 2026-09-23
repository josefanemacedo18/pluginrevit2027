using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class RoomDimensionOptions
    {
        public bool Horizontal { get; set; } = true;
        public bool Vertical { get; set; } = true;
        /// <summary>Linha de cota passando pelo centro do ambiente (senão, junto à parede).</summary>
        public bool AtCenter { get; set; } = true;
        /// <summary>Distância da linha de cota até a parede quando não está no centro (pés).</summary>
        public double WallOffset { get; set; }
        /// <summary>Cria também cadeias com os vãos de portas/janelas ao longo de cada parede.</summary>
        public bool Openings { get; set; }
        /// <summary>Distância da cadeia de vãos até a parede (pés).</summary>
        public double OpeningsOffset { get; set; }
    }

    /// <summary>
    /// Cotas internas de ambientes: uma cadeia em cada direção principal (face a face das
    /// paredes, incluindo pilares e recortes) e, opcionalmente, cadeias com os vãos de portas e
    /// janelas ao longo de cada parede.
    /// </summary>
    public class RoomDimensionService
    {
        private readonly Document _doc;
        private readonly View _view;
        private readonly FaceFinder _faces;
        private readonly DimensionBuilder _builder;
        private readonly Report _report;

        public RoomDimensionService(Document doc, View view, DimensionBuilder builder, Report report)
        {
            _doc = doc;
            _view = view;
            _builder = builder;
            _report = report;
            _faces = new FaceFinder(view);
        }

        private class SegRef
        {
            public Line Line;
            public Element Element;
            public Reference Reference;
            public XYZ Dir;
        }

        public int Dimension(Room room, RoomDimensionOptions o)
        {
            int created = 0;
            List<SegRef> segs = SegmentReferences(room);
            if (segs.Count == 0)
            {
                _report.Warn($"Ambiente {RoomGeo.Label(room)}: contorno sem paredes cotáveis.");
                return 0;
            }

            PlanFrame frame = RoomGeo.Frame(room);
            List<XYZ> boundary = RoomGeo.BoundaryPoints(room);
            double z = _view.GenLevel?.Elevation ?? RoomGeo.BaseElevation(room);
            XYZ center = Geo.WithZ(RoomGeo.Point(room), z);

            var axes = new List<(XYZ d, XYZ e)>();
            if (o.Horizontal) axes.Add((frame.X, frame.Y));
            if (o.Vertical) axes.Add((frame.Y, frame.X));

            foreach ((XYZ d, XYZ e) in axes)
            {
                var refs = segs.Where(s => Geo.IsPerpendicular(s.Dir, d))
                    .Select(s => new RefPos(s.Reference, Geo.Mid(s.Line).DotProduct(d), s.Element))
                    .ToList();
                if (refs.Count < 2) continue;

                XYZ through = center;
                if (!o.AtCenter)
                {
                    double minE = boundary.Min(p => p.DotProduct(e));
                    through = center + e * (minE + o.WallOffset - center.DotProduct(e));
                }

                if (_builder.Create(refs, d, through) != null) created++;
                else _report.Warn($"Ambiente {RoomGeo.Label(room)}: não foi possível criar a cota {(d.IsAlmostEqualTo(frame.X) ? "horizontal" : "vertical")}.");
            }

            if (o.Openings) created += OpeningChains(room, segs, z, o.OpeningsOffset);
            return created;
        }

        /// <summary>Referência (face de acabamento) de cada segmento reto do contorno do ambiente.</summary>
        private List<SegRef> SegmentReferences(Room room)
        {
            var result = new List<SegRef>();
            foreach (IList<BoundarySegment> loop in RoomGeo.Segments(room))
            {
                foreach (BoundarySegment seg in loop)
                {
                    if (!(seg.GetCurve() is Line line) || line.Length < Conv.Cm(1)) continue;
                    Element e = _doc.GetElement(seg.ElementId);
                    if (e == null)
                    {
                        if (seg.LinkElementId != ElementId.InvalidElementId)
                            _report.Warn("Paredes de modelos vinculados não são cotadas automaticamente.");
                        continue;
                    }

                    Reference r = null;
                    if (e is CurveElement ce)
                    {
                        r = ce.GeometryCurve?.Reference;
                    }
                    else
                    {
                        r = _faces.FaceAtSegment(e, line, Conv.Cm(20))?.Reference;
                    }
                    if (r == null) continue;
                    result.Add(new SegRef { Line = line, Element = e, Reference = r, Dir = Geo.FlatDir(line.Direction) });
                }
            }
            return result;
        }

        private int OpeningChains(Room room, List<SegRef> segs, double z, double offset)
        {
            int created = 0;
            foreach (SegRef s in segs)
            {
                if (!(s.Element is Wall wall)) continue;
                XYZ t = s.Dir;
                XYZ a = s.Line.GetEndPoint(0), b = s.Line.GetEndPoint(1);
                double ta = a.DotProduct(t), tb = b.DotProduct(t);
                double tMin = Math.Min(ta, tb), tMax = Math.Max(ta, tb);

                List<FamilyInstance> openings = Q.OpeningsIn(_doc, new[] { wall.Id })
                    .Where(fi =>
                    {
                        XYZ p = Geo.ElementPoint(fi);
                        double tp = p.DotProduct(t);
                        return tp > tMin - Conv.Cm(1) && tp < tMax + Conv.Cm(1);
                    }).ToList();
                if (openings.Count == 0) continue;

                // Normal voltada para dentro do ambiente.
                XYZ n = Geo.LeftNormal(t);
                XYZ mid = Geo.Mid(s.Line);
                if (!RoomGeo.Contains(room, mid + n * Conv.Cm(20))) n = n.Negate();

                var refs = new List<RefPos>();

                // Extremidades: paredes perpendiculares que encostam nas pontas do segmento.
                foreach (SegRef other in segs)
                {
                    if (other == s || !Geo.IsPerpendicular(other.Dir, t)) continue;
                    bool touches = new[] { other.Line.GetEndPoint(0), other.Line.GetEndPoint(1) }
                        .Any(p => Geo.Flat(p).DistanceTo(Geo.Flat(a)) < Conv.Cm(2) || Geo.Flat(p).DistanceTo(Geo.Flat(b)) < Conv.Cm(2));
                    if (touches) refs.Add(new RefPos(other.Reference, Geo.Mid(other.Line).DotProduct(t), other.Element, 1));
                }
                if (refs.Count == 0)
                {
                    _report.Warn($"Ambiente {RoomGeo.Label(room)}: extremidades da parede com vãos não encontradas.");
                }

                // Ombreiras dos vãos (faces da própria parede dentro do segmento).
                XYZ inside = n.Negate() * (wall.Width / 2);
                XYZ la = Geo.WithZ(a, z) + inside, lb = Geo.WithZ(b, z) + inside;
                List<RefPos> jambs = _faces.Crossing(new[] { wall }, la, lb, Conv.Mm(1), 2)
                    .Where(r => r.Position > tMin + Conv.Cm(1) && r.Position < tMax - Conv.Cm(1)).ToList();
                refs.AddRange(OpeningEdges(openings, jambs, t));

                XYZ through = Geo.WithZ(mid, z) + n * offset;
                if (_builder.Create(refs, t, through) != null) created++;
            }
            return created;
        }

        /// <summary>
        /// Seleciona as ombreiras próximas a cada vão; quando a parede não tiver faces de
        /// ombreira, usa as referências esquerda/direita da própria família.
        /// </summary>
        public static List<RefPos> OpeningEdges(IEnumerable<FamilyInstance> openings, List<RefPos> jambs, XYZ t)
        {
            var result = new List<RefPos>();
            foreach (FamilyInstance fi in openings)
            {
                double min, max;
                if (!Geo.ExtentAlong(fi.get_BoundingBox(null), t, out min, out max))
                {
                    double c = Geo.ElementPoint(fi).DotProduct(t), w = Q.OpeningWidth(fi) ?? 0;
                    min = c - w / 2;
                    max = c + w / 2;
                }
                double tol = Conv.Cm(3);
                List<RefPos> near = jambs.Where(j => j.Position >= min - tol && j.Position <= max + tol).ToList();
                if (near.Count >= 2)
                {
                    result.AddRange(near);
                    continue;
                }

                double center = Geo.ElementPoint(fi).DotProduct(t);
                double half = (Q.OpeningWidth(fi) ?? (max - min)) / 2;
                Reference left = fi.GetReferences(FamilyInstanceReferenceType.Left).FirstOrDefault();
                Reference right = fi.GetReferences(FamilyInstanceReferenceType.Right).FirstOrDefault();
                if (left != null && right != null)
                {
                    result.Add(new RefPos(left, center - half, fi, 3));
                    result.Add(new RefPos(right, center + half, fi, 3));
                }
                else
                {
                    result.AddRange(near);
                }
            }
            return result;
        }
    }
}
