using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace DetalhaBIM.Core
{
    /// <summary>Geometria e dados de ambientes (Rooms).</summary>
    public static class RoomGeo
    {
        public static SpatialElementBoundaryOptions FinishOptions() => new SpatialElementBoundaryOptions
        {
            SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish,
        };

        public static bool IsPlaced(Room r) => r != null && r.Location != null && r.Area > Geo.Eps;

        public static string Number(Room r) => Q.Text(r, BuiltInParameter.ROOM_NUMBER);

        public static string Name(Room r) => Q.Text(r, BuiltInParameter.ROOM_NAME);

        public static string Label(Room r)
        {
            string n = Number(r), name = Name(r);
            return string.IsNullOrWhiteSpace(n) ? name : n + " - " + name;
        }

        /// <summary>Aplica o padrão de nome (ex.: "{NUMERO} - {NOME}").</summary>
        public static string FormatName(Room r, string pattern, string suffix)
        {
            string p = string.IsNullOrWhiteSpace(pattern) ? "{NUMERO} - {NOME}" : pattern;
            string level = r.Level?.Name ?? string.Empty;
            string s = p.Replace("{NUMERO}", Number(r)).Replace("{NOME}", Name(r)).Replace("{NIVEL}", level).Trim(' ', '-');
            return string.IsNullOrWhiteSpace(suffix) ? s : s + " - " + suffix;
        }

        public static XYZ Point(Room r) => (r.Location as LocationPoint)?.Point;

        public static double BaseElevation(Room r) => (r.Level?.Elevation ?? 0) + r.BaseOffset;

        public static double TopElevation(Room r)
        {
            double h = r.UnboundedHeight;
            if (h < Conv.Cm(50)) h = Conv.M(2.8);
            return BaseElevation(r) + h;
        }

        public static IList<IList<BoundarySegment>> Segments(Room r)
        {
            return r.GetBoundarySegments(FinishOptions()) ?? new List<IList<BoundarySegment>>();
        }

        /// <summary>Segmentos do contorno externo (maior laço).</summary>
        public static IList<BoundarySegment> OuterSegments(Room r)
        {
            IList<IList<BoundarySegment>> all = Segments(r);
            if (all.Count == 0) return new List<BoundarySegment>();
            return all.OrderByDescending(l => Math.Abs(Geo.PolygonArea(l.Select(s => s.GetCurve().GetEndPoint(0)).ToList()))).First();
        }

        public static List<XYZ> BoundaryPoints(Room r)
        {
            var pts = new List<XYZ>();
            foreach (IList<BoundarySegment> loop in Segments(r))
            foreach (BoundarySegment s in loop)
                pts.AddRange(s.GetCurve().Tessellate());
            return pts;
        }

        /// <summary>Laços do contorno como CurveLoops (externo primeiro), corrigindo pequenas folgas.</summary>
        public static List<CurveLoop> Loops(Room r, double shortCurveTolerance)
        {
            var loops = new List<CurveLoop>();
            foreach (IList<BoundarySegment> segs in Segments(r))
            {
                CurveLoop loop = ToLoop(segs.Select(s => s.GetCurve()).ToList(), shortCurveTolerance);
                if (loop != null) loops.Add(loop);
            }
            return loops.OrderByDescending(Geo.LoopArea).ToList();
        }

        public static CurveLoop ToLoop(IList<Curve> curves, double tol)
        {
            List<Curve> valid = curves.Where(c => c.Length > tol).ToList();
            if (valid.Count < 2) return null;
            try
            {
                return CurveLoop.Create(valid);
            }
            catch
            {
                // Reconstrói ligando extremidades quando houver pequenas descontinuidades.
            }

            try
            {
                var fixedCurves = new List<Curve>();
                var pts = valid.Select(c => c.GetEndPoint(0)).ToList();
                for (int i = 0; i < pts.Count; i++)
                {
                    XYZ a = pts[i], b = pts[(i + 1) % pts.Count];
                    if (a.DistanceTo(b) > tol) fixedCurves.Add(Line.CreateBound(a, b));
                }
                return CurveLoop.Create(fixedCurves);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Sistema local alinhado ao maior segmento reto do contorno.</summary>
        public static PlanFrame Frame(Room r)
        {
            Line longest = OuterSegments(r).Select(s => s.GetCurve()).OfType<Line>()
                .OrderByDescending(l => l.Length).FirstOrDefault();
            XYZ origin = Point(r) ?? XYZ.Zero;
            if (longest == null) return new PlanFrame(origin, XYZ.BasisX);

            // Normaliza a direção para o quadrante mais próximo do eixo X do projeto.
            XYZ d = Geo.FlatDir(longest.Direction);
            double a = Math.Atan2(d.Y, d.X);
            a %= Math.PI / 2;
            if (a < 0) a += Math.PI / 2;
            if (a >= Math.PI / 4) a -= Math.PI / 2;
            return new PlanFrame(origin, new XYZ(Math.Cos(a), Math.Sin(a), 0));
        }

        public static void Bounds(IEnumerable<XYZ> pts, PlanFrame f, out double minX, out double maxX, out double minY, out double maxY)
        {
            minX = minY = double.MaxValue;
            maxX = maxY = double.MinValue;
            foreach (XYZ p in pts)
            {
                double x = f.LX(p), y = f.LY(p);
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        public static bool Contains(Room r, XYZ p)
        {
            return r.IsPointInRoom(new XYZ(p.X, p.Y, BaseElevation(r) + Conv.Cm(50)));
        }
    }
}
