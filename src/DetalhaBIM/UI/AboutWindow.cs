using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DetalhaBIM.Core;
using DetalhaBIM.Ribbon;

namespace DetalhaBIM.UI
{
    /// <summary>Guia rápido de todas as ferramentas, versão e atalhos para pastas do plugin.</summary>
    public class AboutWindow : DetalhaWindow
    {
        public AboutWindow(IntPtr owner)
            : base("DetalhaBIM " + App.Version, "Automação de detalhamento arquitetônico para Autodesk Revit 2027 — cotas, vistas, documentação, organização e modelagem.",
                Theme.Geral, owner, 720)
        {
            SizeToContent = SizeToContent.Manual;
            Height = 700;

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Instalado em: " + System.IO.Path.GetDirectoryName(typeof(App).Assembly.Location),
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 11.5,
                Foreground = Brushes.Gray,
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Dica: passe o mouse sobre qualquer botão da aba DetalhaBIM para ver a descrição completa, ou pressione F1 para abrir o manual.",
                Margin = new Thickness(0, 8, 0, 4),
                Foreground = Brushes.DimGray,
            });

            foreach (PanelDef panel in ToolCatalog.Panels)
            {
                var title = new TextBlock { Text = panel.Name.ToUpperInvariant(), Foreground = Theme.Brush(panel.Color) };
                title.SetResourceReference(StyleProperty, "SectionTitle");
                stack.Children.Add(title);

                foreach (ToolDef tool in ToolCatalog.Tools.Where(t => t.Panel == panel.Name))
                {
                    var row = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };
                    var img = new Image { Source = Icons.Get(tool.Icon, panel.Color, 32), Width = 32, Height = 32, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Top };
                    DockPanel.SetDock(img, Dock.Left);
                    row.Children.Add(img);
                    var text = new StackPanel();
                    text.Children.Add(new TextBlock { Text = tool.Text.Replace("\n", " "), FontWeight = FontWeights.SemiBold });
                    text.Children.Add(new TextBlock { Text = tool.Description, Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x55, 0x63)) });
                    row.Children.Add(text);
                    stack.Children.Add(row);
                }
            }

            SetBody(stack);
            AddButton("Manual online", (s, e) => Open(RibbonBuilder.HelpUrl), left: true);
            AddButton("Pasta de configurações", (s, e) => Open(Logger.AppDataFolder), left: true);
            AddButton("Pasta de logs", (s, e) => Open(Logger.LogFolder), left: true);
            AddButton("Fechar", (s, e) => Close(), primary: true);
        }

        private static void Open(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Error("Abrir " + target, ex);
            }
        }
    }
}
