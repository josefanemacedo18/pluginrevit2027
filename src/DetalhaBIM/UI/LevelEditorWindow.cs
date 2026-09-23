using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DetalhaBIM.Core;

namespace DetalhaBIM.UI
{
    /// <summary>Linha editável do Editor de Níveis.</summary>
    public class LevelRow : INotifyPropertyChanged
    {
        private string _name;
        private string _elevation;
        private bool _floorPlan, _ceilingPlan, _delete;

        public long? LevelId { get; set; }
        public string OriginalName { get; set; }
        public double OriginalElevationM { get; set; }
        public int PlanCount { get; set; }

        public string Name
        {
            get => _name;
            set { _name = value; Changed(nameof(Name)); Changed(nameof(Status)); }
        }

        /// <summary>Elevação em metros (texto, aceita vírgula).</summary>
        public string Elevation
        {
            get => _elevation;
            set { _elevation = value; Changed(nameof(Elevation)); Changed(nameof(Status)); }
        }

        public bool CreateFloorPlan
        {
            get => _floorPlan;
            set { _floorPlan = value; Changed(nameof(CreateFloorPlan)); Changed(nameof(Status)); }
        }

        public bool CreateCeilingPlan
        {
            get => _ceilingPlan;
            set { _ceilingPlan = value; Changed(nameof(CreateCeilingPlan)); Changed(nameof(Status)); }
        }

        public bool Delete
        {
            get => _delete;
            set { _delete = value; Changed(nameof(Delete)); Changed(nameof(Status)); }
        }

        public bool IsNew => LevelId == null;

        public bool Renamed => !IsNew && !string.Equals(Name?.Trim(), OriginalName, StringComparison.Ordinal);

        public bool Moved => !IsNew && Conv.TryParse(Elevation, out double m) && Math.Abs(m - OriginalElevationM) > 1e-6;

        public string Status
        {
            get
            {
                if (Delete) return "Excluir";
                if (IsNew) return "Novo";
                var parts = new List<string>();
                if (Renamed) parts.Add("renomear");
                if (Moved) parts.Add("mover");
                if (CreateFloorPlan || CreateCeilingPlan) parts.Add("criar planta");
                return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Changed(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    /// <summary>
    /// Editor de Níveis: visão de todos os níveis em uma tabela para renomear, mover, criar
    /// plantas, excluir e adicionar níveis, com renomeação em lote.
    /// </summary>
    public class LevelEditorWindow : DetalhaWindow
    {
        private readonly DataGrid _grid;

        public LevelEditorWindow(IEnumerable<LevelRow> rows, IntPtr owner)
            : base("Editor de Níveis", "Edite nomes e elevações diretamente na tabela, crie plantas, exclua ou adicione níveis. As alterações só são aplicadas ao clicar em Aplicar.",
                Theme.Organizacao, owner, 820)
        {
            Rows = new ObservableCollection<LevelRow>(rows);
            SizeToContent = SizeToContent.Manual;
            Height = 620;

            var root = new DockPanel();

            // Barra de ferramentas
            var bar = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
            DockPanel.SetDock(bar, Dock.Top);
            bar.Children.Add(Small("+ Adicionar nível", AddLevel));
            bar.Children.Add(Small("Marcar: criar planta de piso", () => ForSelection(r => r.CreateFloorPlan = true)));
            bar.Children.Add(Small("Marcar: criar planta de forro", () => ForSelection(r => r.CreateCeilingPlan = true)));
            bar.Children.Add(Small("Marcar para excluir", () => ForSelection(r => r.Delete = !r.Delete)));
            root.Children.Add(bar);

            // Renomeação em lote
            var batch = new Expander { Header = "Renomear em lote (aplica às linhas selecionadas ou a todas)", Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(batch, Dock.Top);
            var bp = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            TextBox prefix = Field(bp, "Prefixo", 90), suffix = Field(bp, "Sufixo", 90), find = Field(bp, "Localizar", 110), repl = Field(bp, "Substituir por", 110);
            var upper = new CheckBox { Content = "MAIÚSCULAS", Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            bp.Children.Add(upper);
            bp.Children.Add(Small("Aplicar", () => ForSelection(r =>
            {
                string n = r.Name ?? string.Empty;
                if (find.Text.Length > 0) n = n.Replace(find.Text, repl.Text);
                n = prefix.Text + n + suffix.Text;
                if (upper.IsChecked == true) n = n.ToUpperInvariant();
                r.Name = n;
            })));
            batch.Content = bp;
            root.Children.Add(batch);

            RenameViews = new CheckBox
            {
                Content = "Renomear também as plantas cujo nome contém o nome antigo do nível",
                IsChecked = true,
                Margin = new Thickness(0, 8, 0, 0),
            };
            DockPanel.SetDock(RenameViews, Dock.Bottom);
            root.Children.Add(RenameViews);

            _grid = new DataGrid { ItemsSource = Rows };
            _grid.Columns.Add(new DataGridTextColumn { Header = "Nome", Binding = Bind(nameof(LevelRow.Name)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Elevação (m)", Binding = Bind(nameof(LevelRow.Elevation)), Width = 110 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Plantas", Binding = new Binding(nameof(LevelRow.PlanCount)), IsReadOnly = true, Width = 70 });
            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Criar planta", Binding = Bind(nameof(LevelRow.CreateFloorPlan)), Width = 95 });
            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Criar forro", Binding = Bind(nameof(LevelRow.CreateCeilingPlan)), Width = 90 });
            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Excluir", Binding = Bind(nameof(LevelRow.Delete)), Width = 70 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Alteração", Binding = new Binding(nameof(LevelRow.Status)), IsReadOnly = true, Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
            root.Children.Add(_grid);

            SetBody(root, scroll: false);
            AddButton("Cancelar", (s, e) => DialogResult = false);
            AddButton("Aplicar", (s, e) => Confirm(), primary: true);
        }

        public ObservableCollection<LevelRow> Rows { get; }

        public CheckBox RenameViews { get; }

        private static Binding Bind(string path) => new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };

        private void ForSelection(Action<LevelRow> action)
        {
            _grid.CommitEdit(DataGridEditingUnit.Row, true);
            List<LevelRow> rows = _grid.SelectedItems.OfType<LevelRow>().ToList();
            if (rows.Count == 0) rows = Rows.ToList();
            foreach (LevelRow r in rows) action(r);
        }

        private void AddLevel()
        {
            double top = Rows.Select(r => Conv.TryParse(r.Elevation, out double m) ? m : 0).DefaultIfEmpty(0).Max();
            var row = new LevelRow { Name = "NOVO NÍVEL", Elevation = Conv.Format(top + 3.0), CreateFloorPlan = true };
            Rows.Add(row);
            _grid.SelectedItem = row;
            _grid.ScrollIntoView(row);
        }

        private void Confirm()
        {
            _grid.CommitEdit(DataGridEditingUnit.Row, true);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LevelRow r in Rows.Where(r => !r.Delete))
            {
                if (string.IsNullOrWhiteSpace(r.Name))
                {
                    MessageBox.Show(this, "Há nível sem nome.", "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!names.Add(r.Name.Trim()))
                {
                    MessageBox.Show(this, $"O nome \"{r.Name}\" está repetido. Os nomes de níveis devem ser únicos.", "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!Conv.TryParse(r.Elevation, out _))
                {
                    MessageBox.Show(this, $"Elevação inválida no nível \"{r.Name}\".", "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            DialogResult = true;
        }

        private Button Small(string text, Action action)
        {
            var b = new Button { Content = text, Margin = new Thickness(0, 0, 6, 4), MinWidth = 0 };
            b.Click += (s, e) => action();
            return b;
        }

        private static TextBox Field(Panel parent, string label, double width)
        {
            parent.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 4, 0) });
            var tb = new TextBox { Width = width, Margin = new Thickness(0, 0, 10, 0) };
            parent.Children.Add(tb);
            return tb;
        }
    }
}
