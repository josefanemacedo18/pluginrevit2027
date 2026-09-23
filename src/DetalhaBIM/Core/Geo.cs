using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    /// <summary>
    /// Sistema de coordenadas 2D (em planta) alinhado a uma direção principal — permite tratar
    /// edificações rotacionadas como se fossem ortogonais.
    /// </summary>
    public class PlanFrame
    {
        public PlanFrame(XYZ origin, XYZ xDirection)
        {
            Origin = new XYZ(origin.X, origin.Y, 0);
            XYZ x = Geo.Flat(xDirection);
            X = x.IsZeroLength() ? XYZ.BasisX : x.Normalize();
            Y = XYZ.BasisZ.CrossProduct(X).Normalize();
        }

        public XYZ Origin { get; }
        public XYZ X { get; }
        public XYZ Y { get; }

        public double Angle => Math.Atan2(X.Y, X.X);

        public double LX(XYZ p) => (p - Origin).DotProduct(X);
        public double LY(XYZ p) => (p - Origin).DotProduct(Y);

        public XYZ World(double x, double y, double z) => new XYZ(Origin.X, Origin.Y, z) + X * x + Y * y;

        public static PlanFrame World0 => new PlanFrame(XYZ.Zero, XYZ.BasisX);

        /// <summary>Direção predominante das paredes (módulo 90°), ponderada pelo comprimento.</summary>
        public static PlanFrame FromWalls(IEnumerable<Wall> walls, XYZ origin = null)
        {
            var samples = new List<(double angle, double weight)>();
            foreach (Wall w in walls)
            {
                if (!(w.Location is LocationCurve lc) || !(lc.Curve is Line line)) continue;
                XYZ d = Geo.Flat(line.Direction);
                if (d.IsZeroLength()) continue;
                double a = Math.Atan2(d.Y, d.X);
                a %= Math.PI / 2;
                if (a < 0) a += Math.PI / 2;
                if (a >= Math.PI / 4) a -= Math.PI / 2;
                samples.Add((a, line.Length));
            }
            if (samples.Count == 0) return new PlanFrame(origin ?? XYZ.Zero, XYZ.BasisX);

            double bin = Math.PI / 360; // 0,5°
            var best = samples
                .GroupBy(s => (int)Math.Round(s.angle / bin))
                .OrderByDescending(g => g.Sum(s => s.weight))
                .First();
            double center = best.Key * bin;
            var near = samples.Where(s => Math.Abs(s.angle - center) <= 2 * bin).ToList();
            double angle = near.Sum(s => s.angle * s.weight) / near.Sum(s => s.weight);
            if (Math.Abs(angle) < 1e-4) angle = 0;
            return new PlanFrame(origin ?? XYZ.Zero, new XYZ(Math.Cos(angle), Math.Sin(angle), 0));
        }
    }

    public static class Geo
    {
        public const double Eps = 1e-6;

        public static XYZ Flat(XYZ v) => new XYZ(v.X, v.Y, 0);

        public static XYZ FlatDir(XYZ v)
        {
            XYZ f = Flat(v);
            return f.IsZeroLength() ? XYZ.Zero : f.Normalize();
        }

        public static XYZ WithZ(XYZ p, double z) => new XYZ(p.X, p.Y, z);

        public static bool IsParallel(XYZ a, XYZ b, double cosTol = 0.9998)
        {
            if (a.IsZeroLength() || b.IsZeroLength()) return false;
            return Math.Abs(a.Normalize().DotProduct(b.Normalize())) >= cosTol;
        }

        public static bool IsPerpendicular(XYZ a, XYZ b, double tol = 0.02)
        {
            if (a.IsZeroLength() || b.IsZeroLength()) return false;
            return Math.Abs(a.Normalize().DotProduct(b.Normalize())) <= tol;
        }

        /// <summary>Normal horizontal (à esquerda) de uma direção em planta.</summary>
        public static XYZ LeftNormal(XYZ dir) => XYZ.BasisZ.CrossProduct(FlatDir(dir)).Normalize();

        public static XYZ Mid(Curve c) => c.Evaluate(0.5, true);

        /// <summary>Área (em planta) de uma lista de pontos pela fórmula de Gauss.</summary>
        public static double PolygonArea(IList<XYZ> pts)
        {
            double a = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                XYZ p = pts[i], q = pts[(i + 1) % pts.Count];
                a += p.X * q.Y - q.X * p.Y;
            }
            return a / 2;
        }

        public static List<XYZ> Tessellate(CurveLoop loop)
        {
            var pts = new List<XYZ>();
            foreach (Curve c in loop)
            {
                IList<XYZ> t = c.Tessellate();
                for (int i = 0; i < t.Count - 1; i++) pts.Add(t[i]);
            }
            return pts;
        }

        public static double LoopArea(CurveLoop loop) => Math.Abs(PolygonArea(Tessellate(loop)));

        /// <summary>A, B, ..., Z, AA, AB...</summary>
        public static string Letters(int index)
        {
            string s = string.Empty;
            index++;
            while (index > 0)
            {
                int m = (index - 1) % 26;
                s = (char)('A' + m) + s;
                index = (index - m) / 26;
            }
            return s;
        }

        /// <summary>Ponto representativo de um elemento (ponto de inserção, meio da linha ou centro da caixa).</summary>
        public static XYZ ElementPoint(Element e, View view = null)
        {
            switch (e.Location)
            {
                case LocationPoint lp:
                    return lp.Point;
                case LocationCurve lc:
                    return Mid(lc.Curve);
            }
            BoundingBoxXYZ bb = e.get_BoundingBox(view) ?? e.get_BoundingBox(null);
            return bb == null ? null : (bb.Min + bb.Max) / 2;
        }

        /// <summary>Extensão (mín., máx.) de uma caixa envolvente projetada em uma direção.</summary>
        public static bool ExtentAlong(BoundingBoxXYZ bb, XYZ dir, out double min, out double max)
        {
            min = double.MaxValue;
            max = double.MinValue;
            if (bb == null) return false;
            Transform t = bb.Transform ?? Transform.Identity;
            foreach (double x in new[] { bb.Min.X, bb.Max.X })
            foreach (double y in new[] { bb.Min.Y, bb.Max.Y })
            foreach (double z in new[] { bb.Min.Z, bb.Max.Z })
            {
                double v = t.OfPoint(new XYZ(x, y, z)).DotProduct(dir);
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
            return true;
        }
    }
}
