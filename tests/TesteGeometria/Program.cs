using System;
using System.Collections.Generic;
using System.Linq;
using DetalhaBIM.Core;

namespace DetalhaBIM.TesteGeometria
{
    internal static class Program
    {
        private static int _ok, _falhas;

        private static int Main()
        {
            Run("Áreas e orientação dos laços", Areas);
            Run("Recorte de polígono por retângulo", Recorte);
            Run("Trechos de reta dentro do ambiente (com furo)", Trechos);
            Run("Contagem de peças", Contagem);
            Run("Linhas de junta", Juntas);
            Run("Paginação diagonal (45°) conserva a área", Diagonal);

            Console.WriteLine($"\n=== Resultado: {_ok} verificações OK, {_falhas} falha(s).");
            return _falhas == 0 ? 0 : 1;
        }

        private static void Run(string nome, Action teste)
        {
            Console.WriteLine($"\n--- {nome}");
            try
            {
                teste();
            }
            catch (Exception ex)
            {
                Check(false, $"exceção {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void Check(bool ok, string d)
        {
            if (ok) _ok++;
            else _falhas++;
            Console.WriteLine((ok ? "  [OK]    " : "  [FALHA] ") + d);
        }

        private static bool Near(double a, double b, double tol = 1e-6) => Math.Abs(a - b) <= tol;

        private static List<P2> Rect(double x0, double y0, double x1, double y1) =>
            new List<P2> { new P2(x0, y0), new P2(x1, y0), new P2(x1, y1), new P2(x0, y1) };

        private static List<P2> Rev(List<P2> l)
        {
            var r = new List<P2>(l);
            r.Reverse();
            return r;
        }

        // Sala 3 × 3 m com pilar de 0,6 × 0,6 m encostado no canto (1,2 a 1,8).
        private static readonly List<P2> Sala = Rect(0, 0, 3, 3);
        private static readonly List<P2> Pilar = Rect(1.2, 1.2, 1.8, 1.8);

        // Sala em L: 4 × 2 + 2 × 2 = 12 m².
        private static readonly List<P2> SalaL = new List<P2>
        {
            new P2(0, 0), new P2(4, 0), new P2(4, 2), new P2(2, 2), new P2(2, 4), new P2(0, 4),
        };

        private static void Areas()
        {
            Check(Near(Plano2D.SignedArea(Sala), 9), "Área anti-horária positiva (9 m²)");
            Check(Near(Plano2D.SignedArea(Rev(Sala)), -9), "Área horária negativa");
            Check(Near(Plano2D.NetArea(new[] { Sala, Pilar }), 9 - 0.36), "Área líquida desconta o pilar (8,64 m²)");
            Check(Near(Plano2D.NetArea(new[] { Rev(Sala), Rev(Pilar) }), 8.64), "Independe do sentido dos laços do Revit");
            Check(Near(Plano2D.NetArea(new[] { Pilar, Sala }), 8.64), "Independe da ordem dos laços");
            Check(Near(Plano2D.NetArea(new[] { SalaL }), 12), "Sala em L = 12 m²");
        }

        private static void Recorte()
        {
            Check(Near(Plano2D.SignedArea(Plano2D.ClipRect(Sala, 2.5, 2.5, 3.5, 3.5)), 0.25), "Canto recortado = 0,25 m²");
            Check(Near(Plano2D.SignedArea(Plano2D.ClipRect(SalaL, 1, 1, 3, 3)), 3), "Sala em L (côncava) recortada = 3 m²");
            Check(Plano2D.ClipRect(Sala, 5, 5, 6, 6).Count < 3, "Retângulo fora da sala = vazio");
            Check(Near(Plano2D.SignedArea(Plano2D.ClipRect(Rev(Sala), 0, 0, 1, 1)), -1), "Recorte preserva o sentido (furos continuam negativos)");
        }

        private static void Trechos()
        {
            var loops = Plano2D.Normalize(new[] { Sala, Pilar });
            var t = Plano2D.InsideIntervals(loops, new P2(0, 1.5), new P2(1, 0));
            Check(t.Count == 2, $"Reta que atravessa o pilar gera 2 trechos (gerou {t.Count})");
            Check(t.Count == 2 && Near(t[0].t0, 0) && Near(t[0].t1, 1.2) && Near(t[1].t0, 1.8) && Near(t[1].t1, 3), "Trechos 0–1,2 e 1,8–3");
            var v = Plano2D.InsideIntervals(new[] { SalaL }, new P2(3, -1), new P2(0, 1));
            Check(v.Count == 1 && Near(v[0].t0, 1) && Near(v[0].t1, 3), "Reta na aba da sala em L: trecho y = 0 a 2");
            var vertice = Plano2D.InsideIntervals(new[] { SalaL }, new P2(0, 2), new P2(1, 0));
            Check(vertice.Count == 1 && Near(vertice[0].t1 - vertice[0].t0, 2) || vertice.Count == 1 && Near(vertice[0].t1 - vertice[0].t0, 4),
                "Reta passando exatamente por um vértice não gera trechos falsos");
        }

        private static void Contagem()
        {
            P2 x = new P2(1, 0);
            TileCount a = Plano2D.CountTiles(new[] { Sala }, new P2(0, 0), x, 0.6, 0.6, 0);
            Check(a.Inteiras == 25 && a.Cortadas == 0, $"3×3 m com 60×60 a partir do canto: 25 inteiras, 0 cortadas ({a.Inteiras}/{a.Cortadas})");

            P2 o = Plano2D.GridOrigin(0, new P2(1.5, 1.5), x, 0.6, 0.6, 0);
            TileCount b = Plano2D.CountTiles(new[] { Sala }, o, x, 0.6, 0.6, 0);
            Check(b.Inteiras == 16 && b.Cortadas == 20, $"Junta centralizada: 16 inteiras + 20 cortadas ({b.Inteiras}/{b.Cortadas})");

            P2 o2 = Plano2D.GridOrigin(1, new P2(1.5, 1.5), x, 0.6, 0.6, 0);
            TileCount c = Plano2D.CountTiles(new[] { Sala }, o2, x, 0.6, 0.6, 0);
            Check(c.Inteiras == 25 && c.Cortadas == 0, $"Peça centralizada em sala 3×3 (5 peças exatas): 25 inteiras ({c.Inteiras}/{c.Cortadas})");

            TileCount d = Plano2D.CountTiles(new[] { Sala, Pilar }, new P2(0, 0), x, 0.6, 0.6, 0);
            Check(d.Inteiras == 24 && d.Cortadas == 0, $"Pilar ocupando uma peça: 24 inteiras ({d.Inteiras}/{d.Cortadas})");
            Check(Near(d.AreaRegiao, 8.64), "Área líquida informada = 8,64 m²");

            TileCount e = Plano2D.CountTiles(new[] { Rect(0, 0, 3.05, 3.05) }, new P2(0, 0), x, 0.6, 0.6, 0.005);
            Check(e.Inteiras == 25 && e.Cortadas == 10,
                $"Junta de 5 mm e sobra de 2,5 cm: 25 inteiras + 10 tiras cortadas; canto de 0,2% desprezado ({e.Inteiras}/{e.Cortadas})");

            TileCount f = Plano2D.CountTiles(new[] { SalaL }, new P2(0, 0), x, 1, 1, 0);
            Check(f.Inteiras == 12 && f.Cortadas == 0, $"Sala em L com peças 1×1: 12 inteiras ({f.Inteiras}/{f.Cortadas})");

            TileCount g = Plano2D.CountTiles(new[] { Rect(0, 0, 2.5, 1) }, new P2(0, 0), x, 1.2, 0.2, 0);
            Check(g.Inteiras == 10 && g.Cortadas == 5, $"Régua 120×20 em 2,5×1 m: 10 inteiras + 5 cortadas ({g.Inteiras}/{g.Cortadas})");
        }

        private static void Juntas()
        {
            P2 x = new P2(1, 0);
            var lines = Plano2D.JointLines(new[] { Sala }, new P2(0, 0), x, 0.6, 0.6, 0);
            Check(lines.Count == 8, $"3×3 m, 60×60: 8 linhas de junta ({lines.Count})");
            Check(lines.All(l => Near((l.b - l.a).Length, 3)), "Cada junta atravessa a sala inteira (3 m)");

            var withHole = Plano2D.JointLines(new[] { Sala, Pilar }, new P2(0, 0), x, 0.6, 0.6, 0);
            double total = withHole.Sum(l => (l.b - l.a).Length);
            Check(Near(total, 21.6), $"Juntas que coincidem com as faces do pilar param no pilar: total 21,6 m ({total:0.###})");
            var cruzando = Plano2D.JointLines(new[] { Sala, Rect(1.0, 1.0, 1.4, 1.4) }, new P2(0, 0), x, 0.6, 0.6, 0);
            Check(cruzando.Count == 10, $"Pilar no meio de uma junta divide as 2 juntas que o atravessam (10 trechos: {cruzando.Count})");
            Check(!Plano2D.Inside(new[] { Sala }, new P2(0, 1)) && Plano2D.Inside(new[] { Sala }, new P2(0.1, 1)), "Ponto sobre a parede = fora; ponto interno = dentro");

            var joint = Plano2D.JointLines(new[] { Sala }, new P2(0, 0), x, 0.6, 0.6, 0.004);
            Check(joint.Any(l => Near(l.a.X, 0.602) && Near(l.b.X, 0.602)), "Com junta de 4 mm, a linha fica no eixo da junta (x = 0,602)");
        }

        private static void Diagonal()
        {
            P2 x = P2.FromAngle(Math.PI / 4);
            TileCount r = Plano2D.CountTiles(new[] { Sala }, new P2(1.5, 1.5), x, 0.6, 0.6, 0);
            Check(r.Inteiras * 0.36 <= 9 + 1e-9 && r.Total * 0.36 >= 9 - 1e-9,
                $"Inteiras × área ≤ 9 m² ≤ total × área ({r.Inteiras} inteiras, {r.Total} peças)");
            Check(r.Cortadas > 0, "Paginação a 45° gera peças cortadas nas bordas");
            var lines = Plano2D.JointLines(new[] { Sala }, new P2(1.5, 1.5), x, 0.6, 0.6, 0);
            bool inside = lines.All(l => new[] { l.a, l.b }.All(p => p.X > -1e-6 && p.X < 3 + 1e-6 && p.Y > -1e-6 && p.Y < 3 + 1e-6));
            Check(lines.Count > 0 && inside, $"Juntas diagonais ficam dentro da sala ({lines.Count} linhas)");
        }
    }
}
