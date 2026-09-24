using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DetalhaBIM.Core;
using DetalhaBIM.Ribbon;
using DetalhaBIM.UI;

namespace DetalhaBIM.TesteInterface
{
    internal static class Program
    {
        private static int _ok, _falhas;

        [STAThread]
        private static int Main(string[] args)
        {
            // Proteção: se alguma janela travar, encerra com falha.
            var guarda = new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromMinutes(3));
                Console.WriteLine("  [FALHA] Tempo esgotado (alguma janela não fechou).");
                Environment.Exit(1);
            }) { IsBackground = true };
            guarda.Start();

            string catalogo = args.Length > 0 ? args[0] : null;
            Run("Ícones da faixa de opções", () => TestarIcones(catalogo));
            Run("Tema (XAML embutido)", TestarTema);
            Run("Conversão de números pt-BR", TestarConv);
            Run("Configurações (JSON)", TestarConfiguracoes);
            Run("Diálogo de opções (todas as ferramentas usam)", TestarOptionsDialog);
            Run("Janela de Configurações", TestarSettingsWindow);
            Run("Editor de Níveis", TestarLevelEditor);
            Run("Limpar Ambientes", TestarRoomCleanup);

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
                Check(false, $"{nome}: exceção {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine(ex);
            }
        }

        private static void Check(bool ok, string d)
        {
            if (ok) _ok++;
            else _falhas++;
            Console.WriteLine((ok ? "  [OK]    " : "  [FALHA] ") + d);
        }

        // ------------------------------------------------------------------ ícones

        private static void TestarIcones(string catalogo)
        {
            FieldInfo f = typeof(Icons).GetField("Glyphs", BindingFlags.NonPublic | BindingFlags.Static);
            var glyphs = (IDictionary)f.GetValue(null);
            Check(glyphs.Count >= 20, $"{glyphs.Count} ícones definidos");

            var usados = new List<string>();
            if (catalogo != null && File.Exists(catalogo))
            {
                usados = Regex.Matches(File.ReadAllText(catalogo), @"Icon\s*=\s*""([a-z\-]+)""").Select(m => m.Groups[1].Value).ToList();
                foreach (string k in usados.Distinct())
                    Check(glyphs.Contains(k), $"Ícone \"{k}\" usado na faixa de opções existe");
            }

            foreach (DictionaryEntry e in glyphs)
            {
                string key = (string)e.Key;
                object g = e.Value;
                string stroke = (string)g.GetType().GetProperty("Stroke").GetValue(g);
                string fill = (string)g.GetType().GetProperty("Fill").GetValue(g);
                bool parse = true;
                try
                {
                    if (stroke != null) Geometry.Parse(stroke);
                    if (fill != null) Geometry.Parse(fill);
                }
                catch
                {
                    parse = false;
                }
                Check(parse, $"Ícone \"{key}\": desenho válido no WPF");

                foreach (int size in new[] { 16, 32 })
                {
                    var img = (BitmapSource)Icons.Get(key, Theme.Cotas, size);
                    var px = new byte[size * size * 4];
                    img.CopyPixels(px, size * 4, 0);
                    int brancos = 0, opacos = 0;
                    for (int i = 0; i < px.Length; i += 4)
                    {
                        if (px[i + 3] > 200) opacos++;
                        if (px[i] > 220 && px[i + 1] > 220 && px[i + 2] > 220 && px[i + 3] > 200) brancos++;
                    }
                    Check(img.PixelWidth == size && opacos > size * size / 2 && brancos > size / 2,
                        $"Ícone \"{key}\" {size}px renderizado (fundo {opacos} px, símbolo {brancos} px)");
                }
            }
        }

        // ------------------------------------------------------------------ tema e lógica

        private static void TestarTema()
        {
            ResourceDictionary d = Theme.Dictionary;
            foreach (string k in new[] { "PrimaryButton", "LinkButton", "SectionTitle", "Hint", "BaseButton" })
                Check(d.Contains(k), $"Estilo \"{k}\" presente");
        }

        private static void TestarConv()
        {
            Check(Conv.TryParse("0,50", out double a) && Math.Abs(a - 0.5) < 1e-9, "\"0,50\" = 0,5");
            Check(Conv.TryParse("1.234,5", out double b) && Math.Abs(b - 1234.5) < 1e-9, "\"1.234,5\" = 1234,5");
            Check(!Conv.TryParse("abc", out _), "\"abc\" rejeitado");
        }

        private static void TestarConfiguracoes()
        {
            DetalhaSettings s = SettingsService.Clone();
            Check(s.PlantasTecnicas.Count > 0 && s.Cotas != null && s.Vistas != null, "Configurações padrão carregadas");
            string tmp = Path.Combine(Path.GetTempPath(), "detalhabim-teste.json");
            SettingsService.Export(tmp);
            Check(File.Exists(tmp) && SettingsService.Import(tmp), "Exportar e importar configurações");
        }

        // ------------------------------------------------------------------ janelas

        /// <summary>Abre a janela como diálogo e a fecha sozinha após renderizar.</summary>
        private static bool AbrirEFechar(Window w, Func<bool?> abrir = null)
        {
            bool renderizou = false;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                renderizou = w.IsLoaded && w.ActualWidth > 100 && w.ActualHeight > 100;
                try
                {
                    w.DialogResult = false;
                }
                catch
                {
                    w.Close();
                }
            };
            t.Start();
            if (abrir != null) abrir();
            else w.ShowDialog();
            return renderizou;
        }

        private static void TestarOptionsDialog()
        {
            var dlg = new OptionsDialog("teste-ui", "Teste", "Diálogo de teste com todos os tipos de campo.", Theme.Vistas, IntPtr.Zero);
            FormBuilder f = dlg.Form;
            f.Section("Seção");
            f.Hint("Dica");
            f.Check("c", "Caixa", true);
            f.Number("n", "Número", 8, "mm");
            f.Text("t", "Texto", "abc");
            f.Combo("co", "Lista", new List<string> { "A", "B" }, "B");
            f.Combo("ce", "Lista editável", Choices.Scales, "1:50", true);
            f.Radio("r", "Opções", new[] { "Um", "Dois" }, 1);
            f.Checklist("l", "Itens", Enumerable.Range(1, 50).Select(i => new CheckItem("Item " + i, i, i % 2 == 0, "detalhe")), 120);

            Check(AbrirEFechar(dlg, () => { dlg.Run(); return null; }), "Diálogo de opções abre, renderiza e fecha");
            Check(f.Bool("c") && Math.Abs(f.Double("n") - 8) < 1e-9 && f.String("t") == "abc" && f.String("co") == "B" && f.Index("r") == 1,
                "Leitura dos valores do formulário");
            Check(f.Checked<int>("l").Count == 25, "Lista com caixas de seleção (25 marcados)");
            Check(Choices.ParseScale(f.String("ce"), 0) == 50, "Escala lida da lista editável");
        }

        private static void TestarSettingsWindow()
        {
            var lists = new ProjectLists
            {
                DimensionTypes = new List<string> { "Cota 2mm" },
                PlanTemplates = new List<string> { "ARQ - PLANTA" },
                TitleBlocks = new List<string> { "Carimbo : A1" },
            };
            var w = new SettingsWindow(SettingsService.Clone(), lists, IntPtr.Zero);
            Check(AbrirEFechar(w), "Janela de Configurações abre, renderiza e fecha");
            MethodInfo collect = typeof(SettingsWindow).GetMethod("Collect", BindingFlags.NonPublic | BindingFlags.Instance);
            var s = (DetalhaSettings)collect.Invoke(w, null);
            Check(s != null && s.PlantasTecnicas.Count == 8 && s.Vistas.EscalaPlanta > 0, "Valores das abas lidos corretamente");
        }

        private static void TestarLevelEditor()
        {
            var rows = new[]
            {
                new LevelRow { LevelId = 1, OriginalName = "TÉRREO", Name = "TÉRREO", Elevation = "0,000", OriginalElevationM = 0, PlanCount = 2 },
                new LevelRow { LevelId = 2, OriginalName = "SUPERIOR", Name = "SUPERIOR", Elevation = "3,000", OriginalElevationM = 3, PlanCount = 1 },
            };
            var w = new LevelEditorWindow(rows, IntPtr.Zero);
            Check(AbrirEFechar(w), "Editor de Níveis abre, renderiza e fecha");
            rows[1].Name = "COBERTURA";
            rows[1].Elevation = "6,00";
            Check(rows[1].Renamed && rows[1].Moved && rows[1].Status.Contains("renomear"), "Detecção de alterações nas linhas");
        }

        private static void TestarRoomCleanup()
        {
            var rows = new List<RoomRow>
            {
                new RoomRow { Id = 1, Number = "101", Name = "SALA", Level = "TÉRREO", Status = "OK", Area = "20,00" },
                new RoomRow { Id = 2, Number = "102", Name = "COZINHA", Level = "TÉRREO", Status = "Não delimitado", Area = "—", IsProblem = true, Selected = true },
            };
            var w = new RoomCleanupWindow(rows, IntPtr.Zero);
            Check(AbrirEFechar(w), "Limpar Ambientes abre, renderiza e fecha");
            Check(w.SelectedIds.SequenceEqual(new long[] { 2 }), "Somente o ambiente com problema vem marcado");
        }
    }
}
