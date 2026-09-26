using System;
using System.Collections.Generic;
using System.Linq;

namespace DetalhaBIM.Core
{
    /// <summary>Ponto/vetor 2D. Não depende do Revit, para que a geometria possa ser testada isoladamente.</summary>
    public readonly struct P2
    {
        public P2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public static P2 operator +(P2 a, P2 b) => new P2(a.X + b.X, a.Y + b.Y);
        public static P2 operator -(P2 a, P2 b) => new P2(a.X - b.X, a.Y - b.Y);
        public static P2 operator *(P2 a, double k) => new P2(a.X * k, a.Y * k);

        public double Dot(P2 b) => X * b.X + Y * b.Y;
        public double Cross(P2 b) => X * b.Y - Y * b.X;
        public double Length => Math.Sqrt(X * X + Y * Y);
        public P2 Normalized => Length < 1e-12 ? new P2(1, 0) : this * (1 / Length);
        /// <summary>Vetor girado 90° no sentido anti-horário.</summary>
        public P2 Perp => new P2(-Y, X);

        public static P2 FromAngle(double radians) => new P2(Math.Cos(radians), Math.Sin(radians));

        public override string ToString() => FormattableString.Invariant($"({X:0.###}, {Y:0.###})");
    }

    /// <summary>Uma peça da paginação no sistema da grade: retângulo e fração dentro do ambiente.</summary>
    public readonly struct Tile
    {
        public Tile(int i, int j, double x0, double y0, double x1, double y1, double fraction)
        {
            I = i;
            J = j;
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
            Fraction = fraction;
        }

        public int I { get; }
        public int J { get; }
        public double X0 { get; }
        public double Y0 { get; }
        public double X1 { get; }
        public double Y1 { get; }
        /// <summary>Parte da peça dentro do ambiente (1 = inteira).</summary>
        public double Fraction { get; }
        public bool Whole => Fraction >= 0.999;
    }

    /// <summary>Resultado da contagem de peças de uma paginação.</summary>
    public class TileCount
    {
        public int Inteiras { get; set; }
        public int Cortadas { get; set; }
        public int Total => Inteiras + Cortadas;
        /// <summary>Área líquida da região (mesma unidade² da entrada).</summary>
        public double AreaRegiao { get; set; }
        public double AreaPeca { get; set; }
    }

    /// <summary>
    /// Geometria plana usada pela paginação de piso: áreas, recorte de polígonos, linhas de
    /// junta recortadas pelo contorno do ambiente (com furos) e contagem de peças.
    /// </summary>
    public static class Plano2D
    {
        /// <summary>Área com sinal (positiva = anti-horário).</summary>
        public static double SignedArea(IList<P2> poly)
        {
            double a = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                P2 p = poly[i], q = poly[(i + 1) % poly.Count];
                a += p.X * q.Y - q.X * p.Y;
            }
            return a / 2;
        }

        /// <summary>
        /// Orienta os laços: o maior (contorno externo) no sentido anti-horário e os demais
        /// (pilares, shafts) no sentido horário, para que a soma das áreas com sinal seja a área líquida.
        /// </summary>
        public static List<List<P2>> Normalize(IEnumerable<IList<P2>> loops)
        {
            List<List<P2>> list = loops.Where(l => l != null && l.Count >= 3).Select(Clean).Where(l => l.Count >= 3).ToList();
            if (list.Count == 0) return list;
            int outer = 0;
            for (int i = 1; i < list.Count; i++)
                if (Math.Abs(SignedArea(list[i])) > Math.Abs(SignedArea(list[outer]))) outer = i;
            for (int i = 0; i < list.Count; i++)
            {
                bool ccw = SignedArea(list[i]) > 0;
                bool wantCcw = i == outer;
                if (ccw != wantCcw) list[i].Reverse();
            }
            if (outer != 0)
            {
                List<P2> o = list[outer];
                list.RemoveAt(outer);
                list.Insert(0, o);
            }
            return list;
        }

        public static double NetArea(IEnumerable<IList<P2>> loops) => Normalize(loops).Sum(l => SignedArea(l));

        /// <summary>Remove pontos repetidos consecutivos.</summary>
        private static List<P2> Clean(IList<P2> poly)
        {
            var r = new List<P2>();
            foreach (P2 p in poly)
            {
                if (r.Count > 0 && (p - r[r.Count - 1]).Length < 1e-9) continue;
                r.Add(p);
            }
            if (r.Count > 1 && (r[0] - r[r.Count - 1]).Length < 1e-9) r.RemoveAt(r.Count - 1);
            return r;
        }

        /// <summary>
        /// Recorta um polígono qualquer (côncavo ou não) por um retângulo alinhado aos eixos
        /// (Sutherland–Hodgman). A área com sinal do resultado é a área da interseção.
        /// </summary>
        public static List<P2> ClipRect(IList<P2> poly, double minX, double minY, double maxX, double maxY)
        {
            List<P2> r = poly.ToList();
            r = ClipHalf(r, p => p.X - minX, (a, b) => Lerp(a, b, (minX - a.X) / (b.X - a.X)));
            r = ClipHalf(r, p => maxX - p.X, (a, b) => Lerp(a, b, (maxX - a.X) / (b.X - a.X)));
            r = ClipHalf(r, p => p.Y - minY, (a, b) => Lerp(a, b, (minY - a.Y) / (b.Y - a.Y)));
            r = ClipHalf(r, p => maxY - p.Y, (a, b) => Lerp(a, b, (maxY - a.Y) / (b.Y - a.Y)));
            return r;
        }

        private static P2 Lerp(P2 a, P2 b, double t) => a + (b - a) * t;

        private static List<P2> ClipHalf(List<P2> poly, Func<P2, double> inside, Func<P2, P2, P2> cut)
        {
            var output = new List<P2>();
            if (poly.Count == 0) return output;
            for (int i = 0; i < poly.Count; i++)
            {
                P2 cur = poly[i], prev = poly[(i + poly.Count - 1) % poly.Count];
                bool curIn = inside(cur) >= 0, prevIn = inside(prev) >= 0;
                if (curIn)
                {
                    if (!prevIn) output.Add(cut(prev, cur));
                    output.Add(cur);
                }
                else if (prevIn)
                {
                    output.Add(cut(prev, cur));
                }
            }
            return output;
        }

        /// <summary>
        /// Trechos da reta p(t) = o + d·t que ficam estritamente dentro da região (contorno
        /// externo menos furos). Trechos que coincidem com o contorno (ex.: junta alinhada com a
        /// face de um pilar) não são incluídos. Retorna pares (t0, t1) ordenados.
        /// </summary>
        public static List<(double t0, double t1)> InsideIntervals(IEnumerable<IList<P2>> loops, P2 o, P2 d)
        {
            List<IList<P2>> list = loops.Where(l => l != null && l.Count >= 3).ToList();
            double dd = d.Dot(d);
            var ts = new List<double>();
            foreach (IList<P2> loop in list)
            {
                for (int i = 0; i < loop.Count; i++)
                {
                    P2 a = loop[i], b = loop[(i + 1) % loop.Count];
                    double da = d.Cross(a - o), db = d.Cross(b - o);
                    bool za = Math.Abs(da) < Eps, zb = Math.Abs(db) < Eps;
                    if (za) ts.Add((a - o).Dot(d) / dd);
                    if (zb) ts.Add((b - o).Dot(d) / dd);
                    if (!za && !zb && (da > 0) != (db > 0))
                    {
                        P2 q = a + (b - a) * (da / (da - db));
                        ts.Add((q - o).Dot(d) / dd);
                    }
                }
            }
            ts.Sort();

            var result = new List<(double, double)>();
            for (int i = 0; i + 1 < ts.Count; i++)
            {
                double t0 = ts[i], t1 = ts[i + 1];
                if (t1 - t0 < Eps) continue;
                if (!Inside(list, o + d * ((t0 + t1) / 2))) continue;
                if (result.Count > 0 && Math.Abs(result[result.Count - 1].Item2 - t0) < Eps)
                    result[result.Count - 1] = (result[result.Count - 1].Item1, t1);
                else
                    result.Add((t0, t1));
            }
            return result;
        }

        private const double Eps = 1e-9;

        /// <summary>Ponto estritamente dentro da região (regra par-ímpar; sobre o contorno = fora).</summary>
        public static bool Inside(IEnumerable<IList<P2>> loops, P2 p)
        {
            bool inside = false;
            foreach (IList<P2> loop in loops)
            {
                for (int i = 0; i < loop.Count; i++)
                {
                    P2 a = loop[i], b = loop[(i + 1) % loop.Count];
                    if (DistanceToSegment(p, a, b) < 1e-7) return false;
                    if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y)) inside = !inside;
                }
            }
            return inside;
        }

        private static double DistanceToSegment(P2 p, P2 a, P2 b)
        {
            P2 ab = b - a;
            double len2 = ab.Dot(ab);
            double t = len2 < 1e-18 ? 0 : Math.Max(0, Math.Min(1, (p - a).Dot(ab) / len2));
            return (p - (a + ab * t)).Length;
        }

        /// <summary>Converte os laços para o sistema da grade (origem e eixo X da paginação).</summary>
        public static List<List<P2>> ToGrid(IEnumerable<IList<P2>> loops, P2 origin, P2 xAxis)
        {
            P2 x = xAxis.Normalized, y = x.Perp;
            return loops.Select(l => l.Select(p => new P2((p - origin).Dot(x), (p - origin).Dot(y))).ToList()).ToList();
        }

        public static P2 FromGrid(P2 g, P2 origin, P2 xAxis)
        {
            P2 x = xAxis.Normalized, y = x.Perp;
            return origin + x * g.X + y * g.Y;
        }

        private static void Bounds(IEnumerable<IList<P2>> loops, out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = minY = double.MaxValue;
            maxX = maxY = double.MinValue;
            foreach (IList<P2> l in loops)
            foreach (P2 p in l)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        /// <summary>
        /// Conta as peças de uma paginação. A peça (i, j) ocupa, no sistema da grade,
        /// [i·(w+junta), i·(w+junta)+w] × [j·(h+junta), j·(h+junta)+h]. Peças com menos de
        /// <paramref name="minFraction"/> da área dentro do ambiente são desprezadas (resolvidas no rejunte).
        /// </summary>
        public static TileCount CountTiles(IEnumerable<IList<P2>> loops, P2 origin, P2 xAxis, double w, double h, double joint, double minFraction = 0.005)
        {
            List<List<P2>> grid = Normalize(ToGrid(loops, origin, xAxis));
            var result = new TileCount { AreaPeca = w * h, AreaRegiao = grid.Sum(l => SignedArea(l)) };
            foreach (Tile t in Tiles(loops, origin, xAxis, w, h, joint, minFraction))
            {
                if (t.Whole) result.Inteiras++;
                else result.Cortadas++;
            }
            return result;
        }

        /// <summary>
        /// Peças da paginação que ficam (no todo ou em parte) dentro do ambiente, no sistema da grade.
        /// A peça (i, j) ocupa [i·(w+junta), i·(w+junta)+w] × [j·(h+junta), j·(h+junta)+h].
        /// Peças com menos de <paramref name="minFraction"/> da área dentro são desprezadas
        /// (resolvidas no rejunte).
        /// </summary>
        public static List<Tile> Tiles(IEnumerable<IList<P2>> loops, P2 origin, P2 xAxis, double w, double h, double joint, double minFraction = 0.005)
        {
            var result = new List<Tile>();
            List<List<P2>> grid = Normalize(ToGrid(loops, origin, xAxis));
            if (grid.Count == 0 || w <= 0 || h <= 0) return result;

            double px = w + Math.Max(0, joint), py = h + Math.Max(0, joint);
            Bounds(grid, out double minX, out double minY, out double maxX, out double maxY);
            int i0 = (int)Math.Floor(minX / px) - 1, i1 = (int)Math.Ceiling(maxX / px) + 1;
            int j0 = (int)Math.Floor(minY / py) - 1, j1 = (int)Math.Ceiling(maxY / py) + 1;

            var boxes = grid.Select(l =>
            {
                Bounds(new[] { l }, out double a, out double b, out double c, out double d);
                return (a, b, c, d);
            }).ToList();

            for (int i = i0; i <= i1; i++)
            {
                double x0 = i * px, x1 = x0 + w;
                if (x1 < minX || x0 > maxX) continue;
                for (int j = j0; j <= j1; j++)
                {
                    double y0 = j * py, y1 = y0 + h;
                    if (y1 < minY || y0 > maxY) continue;
                    double area = 0;
                    for (int k = 0; k < grid.Count; k++)
                    {
                        (double a, double b, double c, double d) = boxes[k];
                        if (c < x0 || a > x1 || d < y0 || b > y1) continue;
                        List<P2> clipped = ClipRect(grid[k], x0, y0, x1, y1);
                        if (clipped.Count >= 3) area += SignedArea(clipped);
                    }
                    double frac = area / (w * h);
                    if (frac > minFraction) result.Add(new Tile(i, j, x0, y0, x1, y1, Math.Min(1, frac)));
                }
            }
            return result;
        }

        /// <summary>Posições (no sistema da grade) das linhas de junta que cortam a região, nos dois eixos.</summary>
        public static (List<double> xs, List<double> ys) JointPositions(IEnumerable<IList<P2>> loops, P2 origin, P2 xAxis, double w, double h, double joint)
        {
            var xs = new List<double>();
            var ys = new List<double>();
            List<List<P2>> grid = ToGrid(loops, origin, xAxis);
            if (grid.Count == 0 || w <= 0 || h <= 0) return (xs, ys);
            double j = Math.Max(0, joint);
            double px = w + j, py = h + j;
            Bounds(grid, out double minX, out double minY, out double maxX, out double maxY);
            for (int i = (int)Math.Floor((minX - w) / px) - 1; i <= (int)Math.Ceiling(maxX / px) + 1; i++)
            {
                double x = i * px + w + j / 2;
                if (x > minX && x < maxX) xs.Add(x);
            }
            for (int k = (int)Math.Floor((minY - h) / py) - 1; k <= (int)Math.Ceiling(maxY / py) + 1; k++)
            {
                double y = k * py + h + j / 2;
                if (y > minY && y < maxY) ys.Add(y);
            }
            return (xs, ys);
        }

        /// <summary>
        /// Verifica se a direção <paramref name="q"/> (a partir do centro) está dentro do trecho de
        /// arco que vai de <paramref name="p0"/> a <paramref name="p1"/> passando por <paramref name="pm"/>.
        /// </summary>
        public static bool OnArc(P2 p0, P2 pm, P2 p1, P2 q)
        {
            double a0 = Math.Atan2(p0.Y, p0.X), am = Math.Atan2(pm.Y, pm.X), a1 = Math.Atan2(p1.Y, p1.X), aq = Math.Atan2(q.Y, q.X);
            double sweep = Norm(a1 - a0);
            bool ccw = Norm(am - a0) < sweep;
            if (ccw) return Norm(aq - a0) <= sweep + 1e-9;
            return Norm(aq - a1) <= Norm(a0 - a1) + 1e-9;
        }

        private static double Norm(double a)
        {
            double t = a % (2 * Math.PI);
            return t < 0 ? t + 2 * Math.PI : t;
        }

        /// <summary>
        /// Linhas de junta (no eixo de cada junta) recortadas pela região, no sistema de
        /// coordenadas original.
        /// </summary>
        public static List<(P2 a, P2 b)> JointLines(IEnumerable<IList<P2>> loops, P2 origin, P2 xAxis, double w, double h, double joint)
        {
            var result = new List<(P2, P2)>();
            List<List<P2>> grid = ToGrid(loops, origin, xAxis);
            if (grid.Count == 0 || w <= 0 || h <= 0) return result;
            double j = Math.Max(0, joint);
            double px = w + j, py = h + j;
            Bounds(grid, out double minX, out double minY, out double maxX, out double maxY);

            // Juntas verticais (paralelas ao eixo Y da grade).
            for (int i = (int)Math.Floor((minX - w) / px) - 1; i <= (int)Math.Ceiling(maxX / px) + 1; i++)
            {
                double x = i * px + w + j / 2;
                if (x <= minX || x >= maxX) continue;
                foreach ((double t0, double t1) in InsideIntervals(grid, new P2(x, 0), new P2(0, 1)))
                    result.Add((FromGrid(new P2(x, t0), origin, xAxis), FromGrid(new P2(x, t1), origin, xAxis)));
            }
            // Juntas horizontais.
            for (int k = (int)Math.Floor((minY - h) / py) - 1; k <= (int)Math.Ceiling(maxY / py) + 1; k++)
            {
                double y = k * py + h + j / 2;
                if (y <= minY || y >= maxY) continue;
                foreach ((double t0, double t1) in InsideIntervals(grid, new P2(0, y), new P2(1, 0)))
                    result.Add((FromGrid(new P2(t0, y), origin, xAxis), FromGrid(new P2(t1, y), origin, xAxis)));
            }
            return result;
        }

        /// <summary>
        /// Origem da grade conforme o ponto de partida: 0 = junta centralizada em <paramref name="c"/>,
        /// 1 = peça centralizada em <paramref name="c"/>, 2 = <paramref name="c"/> é o canto de uma peça.
        /// </summary>
        public static P2 GridOrigin(int mode, P2 c, P2 xAxis, double w, double h, double joint)
        {
            P2 x = xAxis.Normalized, y = x.Perp;
            switch (mode)
            {
                case 0:
                    return c - x * (w + joint / 2) - y * (h + joint / 2);
                case 1:
                    return c - x * (w / 2) - y * (h / 2);
                default:
                    return c;
            }
        }
    }
}
