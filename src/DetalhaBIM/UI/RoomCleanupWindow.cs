using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DetalhaBIM.UI
{
    public class RoomRow : INotifyPropertyChanged
    {
        private bool _selected;

        public long Id { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
        public string Level { get; set; }
        public string Status { get; set; }
        public string Area { get; set; }
        public bool IsProblem { get; set; }

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>Lista ambientes não colocados, não delimitados e redundantes para exclusão definitiva.</summary>
    public class RoomCleanupWindow : DetalhaWindow
    {
        private readonly List<RoomRow> _all;
        private readonly ObservableCollection<RoomRow> _rows = new ObservableCollection<RoomRow>();
        private readonly CheckBox _showAll;

        public RoomCleanupWindow(List<RoomRow> rows, IntPtr owner)
            : base("Limpar Ambientes", "Localiza ambientes não colocados, não delimitados e redundantes. Marque os que deseja excluir definitivamente do projeto.",
                Theme.Organizacao, owner, 760)
        {
            _all = rows;
            SizeToContent = SizeToContent.Manual;
            Height = 560;

            var root = new DockPanel();
            var bar = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
            DockPanel.SetDock(bar, Dock.Top);
            _showAll = new CheckBox { Content = "Mostrar também ambientes sem problemas", Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
            _showAll.Checked += (s, e) => Refresh();
            _showAll.Unchecked += (s, e) => Refresh();
            bar.Children.Add(_showAll);
            bar.Children.Add(Small("Marcar problemáticos", () => { foreach (RoomRow r in _rows) r.Selected = r.IsProblem; }));
            bar.Children.Add(Small("Desmarcar todos", () => { foreach (RoomRow r in _rows) r.Selected = false; }));
            root.Children.Add(bar);

            var summary = new TextBlock
            {
                Text = $"{rows.Count(r => r.IsProblem)} ambiente(s) com problema de {rows.Count} no projeto.",
                Margin = new Thickness(0, 8, 0, 0),
                Foreground = System.Windows.Media.Brushes.DimGray,
            };
            DockPanel.SetDock(summary, Dock.Bottom);
            root.Children.Add(summary);

            var grid = new DataGrid { ItemsSource = _rows };
            grid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "Excluir",
                Binding = new Binding(nameof(RoomRow.Selected)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = 64,
            });
            grid.Columns.Add(Col("Número", nameof(RoomRow.Number), 80));
            grid.Columns.Add(Col("Nome", nameof(RoomRow.Name), 0));
            grid.Columns.Add(Col("Nível", nameof(RoomRow.Level), 130));
            grid.Columns.Add(Col("Situação", nameof(RoomRow.Status), 150));
            grid.Columns.Add(Col("Área (m²)", nameof(RoomRow.Area), 80));
            root.Children.Add(grid);

            SetBody(root, scroll: false);
            AddButton("Cancelar", (s, e) => DialogResult = false);
            AddButton("Excluir marcados", (s, e) =>
            {
                if (!_all.Any(r => r.Selected))
                {
                    MessageBox.Show(this, "Nenhum ambiente marcado.", "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                DialogResult = true;
            }, primary: true);
            Refresh();
        }

        public IEnumerable<long> SelectedIds => _all.Where(r => r.Selected).Select(r => r.Id);

        private void Refresh()
        {
            _rows.Clear();
            foreach (RoomRow r in _all.Where(r => _showAll.IsChecked == true || r.IsProblem)) _rows.Add(r);
        }

        private static DataGridTextColumn Col(string header, string path, double width)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path),
                IsReadOnly = true,
                Width = width > 0 ? new DataGridLength(width) : new DataGridLength(1, DataGridLengthUnitType.Star),
            };
        }

        private static Button Small(string text, Action action)
        {
            var b = new Button { Content = text, Margin = new Thickness(0, 0, 6, 0), MinWidth = 0 };
            b.Click += (s, e) => action();
            return b;
        }
    }
}
