using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DetalhaBIM.Ribbon
{
    /// <summary>
    /// Ícones vetoriais desenhados em tempo de execução (grade 24×24): um quadrado arredondado
    /// na cor do painel com o símbolo em branco. Dispensa arquivos de imagem e fica nítido.
    /// </summary>
    public static class Icons
    {
        private class Glyph
        {
            public Glyph(string stroke, string fill = null)
            {
                Stroke = stroke;
                Fill = fill;
            }

            public string Stroke { get; }
            public string Fill { get; }
        }

        // Sempre com cultura invariável: em Windows pt-BR "2.5" viraria "2,5" e quebraria o caminho.
        private static string Circle(double cx, double cy, double r) =>
            System.FormattableString.Invariant($"M{cx},{cy} m-{r},0 a{r},{r} 0 1,0 {2 * r},0 a{r},{r} 0 1,0 -{2 * r},0 ");

        private static readonly Dictionary<string, Glyph> Glyphs = new Dictionary<string, Glyph>
        {
            ["cotas-ambiente"] = new Glyph("M4,5 H20 V19 H4 Z M7,12 H17 M7,9.5 V14.5 M17,9.5 V14.5"),
            ["cotas-parede"] = new Glyph("M3,8 H21 M3,5.5 V11 M21,5.5 V11 M10,5.5 V11 M15,5.5 V11", "M3,15 H21 V19 H3 Z"),
            ["cotas-externas"] = new Glyph("M8,8 H18 V18 H8 Z M8,3.5 H18 M8,2 V5 M18,2 V5 M3.5,8 V18 M2,8 H5 M2,18 H5"),
            ["cotas-selecao"] = new Glyph("M3,6 H21 M3,3.5 V8.5 M21,3.5 V8.5", "M9,10 L9,22 L12,19.2 L14.2,23.5 L16.2,22.6 L14,18.3 L18,18.3 Z"),
            ["cotas-pontos"] = new Glyph("M5,9 H19 M5,6.5 V11.5 M19,6.5 V11.5", Circle(5, 17, 2.5) + Circle(19, 17, 2.5)),
            ["niveis-ambiente"] = new Glyph("M3,15 H21 M12,9 V4 H20", "M8.5,9 H15.5 L12,15 Z"),
            ["vistas-ambiente"] = new Glyph("M3,3 H13 V13 H3 Z M16,3 H21 V8 H16 Z M16,11 H21 V16 H16 Z M3,16 H13 V21 H3 Z M16,19 H21"),
            ["isometrico"] = new Glyph("M12,3 L20,7.5 V16.5 L12,21 L4,16.5 V7.5 Z M4,7.5 L12,12 L20,7.5 M12,12 V21"),
            ["elevacoes"] = new Glyph(Circle(9.5, 12, 6.5), "M16.5,5.5 L23,12 L16.5,18.5 Z"),
            ["plantas-tecnicas"] = new Glyph("M4,7 H16 V21 H4 Z M8,3 H20 V17 H16 M7,11 H13 M7,14 H13 M7,17 H11"),
            ["esquadrias"] = new Glyph("M5,3 H19 V21 H5 Z M12,3 V21 M5,12 H19"),
            ["etiquetar"] = new Glyph("M3,12 L8.5,6 H21 V18 H8.5 Z", Circle(9, 12, 1.6)),
            ["quadros"] = new Glyph("M3,4 H21 V20 H3 Z M3,9 H21 M3,14.5 H21 M9,4 V20"),
            ["pranchas"] = new Glyph("M3,5 H21 V19 H3 Z M15,5 V19 M15,15 H21 M6,8 H12 V14 H6 Z"),
            ["renumerar"] = new Glyph("M9.5,3 L7.5,21 M16.5,3 L14.5,21 M4,8.5 H20 M3.5,15.5 H19.5"),
            ["niveis"] = new Glyph("M3,6 H16 M3,12 H16 M3,18 H16", "M16,6 L20.5,3.5 V8.5 Z M16,12 L20.5,9.5 V14.5 Z M16,18 L20.5,15.5 V20.5 Z"),
            ["eixos"] = new Glyph("M7,8 V22 M17,8 V22 M2,15 H22 " + Circle(7, 5, 2.8) + Circle(17, 5, 2.8)),
            ["remover-ambientes"] = new Glyph("M4,4 H20 V20 H4 Z M9,9 L15,15 M15,9 L9,15"),
            ["forros"] = new Glyph("M2,4 H22 V10 H2 Z M8.5,4 V10 M15.5,4 V10 M4,10 V20 M20,10 V20 M4,20 H20"),
            ["pisos"] = new Glyph("M3,14 H21 V19 H3 Z M7,14 L4,17 M11,14 L6.5,19 M15,14 L10.5,19 M19,14 L14.5,19 M21,16 L18.5,19 M4,11 V5 M20,11 V5"),
            ["luminarias"] = new Glyph("M3,4 H21 M12,4 V8 M12,17 V20.5 M6.5,16 L4.5,18.5 M17.5,16 L19.5,18.5", "M6.5,14 A5.5,5.5 0 0,1 17.5,14 Z"),
            ["config"] = new Glyph("M4,6 H20 M4,12 H20 M4,18 H20", Circle(9, 6, 2.3) + Circle(15, 12, 2.3) + Circle(7, 18, 2.3)),
            ["sobre"] = new Glyph(Circle(12, 12, 9) + "M12,11 V17", Circle(12, 7.6, 1.4)),
        };

        private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>();

        public static ImageSource Get(string key, Color color, int size)
        {
            string cacheKey = $"{key}|{color}|{size}";
            if (Cache.TryGetValue(cacheKey, out ImageSource img)) return img;

            Glyphs.TryGetValue(key, out Glyph glyph);
            var dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                var bg = new LinearGradientBrush(Lighten(color, 0.18), color, 90);
                double r = size * 0.22;
                dc.DrawRoundedRectangle(bg, null, new Rect(0.5, 0.5, size - 1, size - 1), r, r);

                if (glyph != null)
                {
                    double scale = size * 0.76 / 24.0;
                    double offset = (size - 24 * scale) / 2;
                    dc.PushTransform(new TranslateTransform(offset, offset));
                    dc.PushTransform(new ScaleTransform(scale, scale));
                    var pen = new Pen(Brushes.White, size >= 32 ? 1.9 : 2.3)
                    {
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round,
                        LineJoin = PenLineJoin.Round,
                    };
                    if (glyph.Stroke != null) dc.DrawGeometry(null, pen, Geometry.Parse(glyph.Stroke));
                    if (glyph.Fill != null) dc.DrawGeometry(Brushes.White, null, Geometry.Parse(glyph.Fill));
                    dc.Pop();
                    dc.Pop();
                }
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            Cache[cacheKey] = bmp;
            return bmp;
        }

        private static Color Lighten(Color c, double amount)
        {
            byte L(byte v) => (byte)System.Math.Min(255, v + (255 - v) * amount);
            return Color.FromRgb(L(c.R), L(c.G), L(c.B));
        }
    }
}
