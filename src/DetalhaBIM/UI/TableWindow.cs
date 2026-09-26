using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DetalhaBIM.UI
{
    /// <summary>
    /// Tabela de resultados (quantitativos, paginações...) com cópia para o Excel e exportação CSV
    /// no formato brasileiro (separador ";" e vírgula decimal, abre direto no Excel).
    /// </summary>
    public class TableWindow : DetalhaWindow
    {
        private readonly IList<string> _headers;
        private readonly IList<string[]> _rows;
        private readonly string _fileName;

        public TableWindow(string title, string subtitle, Color accent, IntPtr owner, IList<string> headers, IList<string[]> rows, string fileName, string footer = null)
            : base(title, subtitle, accent, owner, 900)
        {
            _headers = headers;
            _rows = rows;
            _fileName = string.IsNullOrWhiteSpace(fileName) ? "DetalhaBIM.csv" : fileName;
            SizeToContent = SizeToContent.Manual;
            Height = 560;

            var root = new DockPanel();
            if (!string.IsNullOrWhiteSpace(footer))
            {
                var note = new TextBlock { Text = footer, Margin = new Thickness(0, 8, 0, 0), Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap };
                DockPanel.SetDock(note, Dock.Bottom);
                root.Children.Add(note);
            }

            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0xE3, 0xE7, 0xEC)),
            };
            for (int i = 0; i < headers.Count; i++)
            {
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = headers[i],
                    Binding = new Binding($"[{i}]"),
                    Width = i == 0 ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto,
                    MinWidth = i == 0 ? 160 : 60,
                });
            }
            root.Children.Add(grid);
            Grid = grid;

            SetBody(root, scroll: false);
            AddButton("Copiar para o Excel", (s, e) => Copy(), left: true);
            AddButton("Exportar CSV...", (s, e) => Export());
            AddButton("Fechar", (s, e) => Close(), primary: true);
        }

        public DataGrid Grid { get; }

        /// <summary>CSV com ";" (padrão do Excel em português), aspas quando necessário.</summary>
        public static string ToCsv(IList<string> headers, IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", headers.Select(Cell)));
            foreach (string[] r in rows) sb.AppendLine(string.Join(";", r.Select(Cell)));
            return sb.ToString();
        }

        /// <summary>Texto separado por tabulação (colar no Excel/Planilhas).</summary>
        public static string ToTsv(IList<string> headers, IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join("\t", headers));
            foreach (string[] r in rows) sb.AppendLine(string.Join("\t", r.Select(c => (c ?? string.Empty).Replace('\t', ' ').Replace('\n', ' '))));
            return sb.ToString();
        }

        private static string Cell(string c)
        {
            c ??= string.Empty;
            return c.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + c.Replace("\"", "\"\"") + "\"" : c;
        }

        private void Copy()
        {
            try
            {
                Clipboard.SetText(ToTsv(_headers, _rows));
                MessageBox.Show(this, "Tabela copiada. Cole no Excel com Ctrl+V.", "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível copiar: " + ex.Message, "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Export()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = _fileName,
                DefaultExt = ".csv",
                Filter = "Planilha CSV (*.csv)|*.csv",
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                // UTF-8 com BOM: o Excel reconhece os acentos.
                File.WriteAllText(dlg.FileName, ToCsv(_headers, _rows), new UTF8Encoding(true));
                MessageBox.Show(this, "Arquivo salvo:\n" + dlg.FileName, "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível salvar: " + ex.Message, "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
