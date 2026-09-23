using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using DetalhaBIM.Core;

namespace DetalhaBIM.UI
{
    /// <summary>Item de uma lista com caixas de seleção.</summary>
    public class CheckItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public CheckItem(string text, object tag, bool isChecked = false, string detail = null)
        {
            Text = text;
            Tag = tag;
            _isChecked = isChecked;
            Detail = detail ?? string.Empty;
        }

        public string Text { get; }
        public string Detail { get; }
        public object Tag { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Monta formulários de opções (seções, caixas, números, listas) de forma declarativa e
    /// lembra os últimos valores usados em cada ferramenta.
    /// </summary>
    public class FormBuilder
    {
        private readonly string _stateId;
        private readonly Dictionary<string, Func<string>> _getters = new Dictionary<string, Func<string>>();
        private readonly Dictionary<string, Action<string>> _setters = new Dictionary<string, Action<string>>();
        private readonly Dictionary<string, string> _defaults = new Dictionary<string, string>();
        private readonly Dictionary<string, TextBox> _numbers = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, ObservableCollection<CheckItem>> _lists = new Dictionary<string, ObservableCollection<CheckItem>>();
        private readonly Dictionary<string, List<RadioButton>> _radios = new Dictionary<string, List<RadioButton>>();

        public FormBuilder(string stateId)
        {
            _stateId = stateId;
            Panel = new StackPanel();
        }

        public StackPanel Panel { get; }

        public void Section(string title)
        {
            var tb = new TextBlock { Text = title };
            tb.SetResourceReference(FrameworkElement.StyleProperty, "SectionTitle");
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE1, 0xE5, 0xEB)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 4, 0, 6),
                Child = tb,
            };
            Panel.Children.Add(border);
        }

        public void Hint(string text)
        {
            var tb = new TextBlock { Text = text };
            tb.SetResourceReference(FrameworkElement.StyleProperty, "Hint");
            Panel.Children.Add(tb);
        }

        public CheckBox Check(string key, string label, bool value, string tooltip = null)
        {
            var cb = new CheckBox { Content = new TextBlock { Text = label }, IsChecked = value, ToolTip = tooltip };
            Panel.Children.Add(cb);
            Register(key, () => cb.IsChecked == true ? "1" : "0", v => cb.IsChecked = v == "1");
            return cb;
        }

        public TextBox Number(string key, string label, double value, string unit = null, string tooltip = null)
        {
            var tb = new TextBox { Text = Conv.Format(value, value % 1 == 0 ? 0 : 2), ToolTip = tooltip, Width = 110, HorizontalAlignment = HorizontalAlignment.Left };
            FrameworkElement control = tb;
            if (!string.IsNullOrEmpty(unit))
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(tb);
                sp.Children.Add(new TextBlock { Text = unit, Margin = new Thickness(8, 0, 0, 0), Foreground = Brushes.DimGray });
                control = sp;
            }
            AddRow(label, control, tooltip);
            _numbers[key] = tb;
            Register(key, () => tb.Text, v => tb.Text = v);
            return tb;
        }

        public TextBox Text(string key, string label, string value, string tooltip = null)
        {
            var tb = new TextBox { Text = value ?? string.Empty, ToolTip = tooltip };
            AddRow(label, tb, tooltip);
            Register(key, () => tb.Text, v => tb.Text = v);
            return tb;
        }

        public ComboBox Combo(string key, string label, IList<string> items, string selected, bool editable = false, string tooltip = null)
        {
            var cb = new ComboBox { IsEditable = editable, ToolTip = tooltip };
            foreach (string i in items) cb.Items.Add(i);
            SelectText(cb, selected);
            if (cb.SelectedIndex < 0 && cb.Items.Count > 0 && !editable) cb.SelectedIndex = 0;
            AddRow(label, cb, tooltip);
            Register(key, () => editable ? cb.Text : cb.SelectedItem as string ?? string.Empty, v => SelectText(cb, v));
            return cb;
        }

        public void Radio(string key, string label, IList<string> options, int selected)
        {
            if (!string.IsNullOrEmpty(label))
            {
                Panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 4, 0, 2) });
            }
            var group = key + Guid.NewGuid().ToString("N");
            var buttons = new List<RadioButton>();
            var sp = new StackPanel { Margin = new Thickness(8, 0, 0, 4) };
            for (int i = 0; i < options.Count; i++)
            {
                var rb = new RadioButton { Content = new TextBlock { Text = options[i] }, GroupName = group, IsChecked = i == selected };
                buttons.Add(rb);
                sp.Children.Add(rb);
            }
            Panel.Children.Add(sp);
            _radios[key] = buttons;
            Register(key,
                () => buttons.FindIndex(b => b.IsChecked == true).ToString(),
                v =>
                {
                    if (int.TryParse(v, out int idx) && idx >= 0 && idx < buttons.Count) buttons[idx].IsChecked = true;
                });
        }

        /// <summary>Lista com caixas de seleção, busca e botões Todos/Nenhum.</summary>
        public ObservableCollection<CheckItem> Checklist(string key, string label, IEnumerable<CheckItem> items, double height = 190)
        {
            var collection = new ObservableCollection<CheckItem>(items);
            _lists[key] = collection;

            if (!string.IsNullOrEmpty(label)) Panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 6, 0, 4) });

            var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(buttons, Dock.Right);
            var search = new TextBox { ToolTip = "Filtrar a lista" };
            var hint = new TextBlock { Text = "🔍 Filtrar...", Foreground = Brushes.Gray, IsHitTestVisible = false, Margin = new Thickness(8, 0, 0, 0) };
            var searchGrid = new Grid();
            searchGrid.Children.Add(search);
            searchGrid.Children.Add(hint);

            ICollectionView view = CollectionViewSource.GetDefaultView(collection);
            search.TextChanged += (s, e) =>
            {
                string q = search.Text.Trim();
                hint.Visibility = q.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                view.Filter = q.Length == 0 ? null : new Predicate<object>(o => ((CheckItem)o).Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                                                                            || ((CheckItem)o).Detail.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            };

            Button all = MakeLink("Todos", () => { foreach (CheckItem i in view.Cast<CheckItem>()) i.IsChecked = true; });
            Button none = MakeLink("Nenhum", () => { foreach (CheckItem i in view.Cast<CheckItem>()) i.IsChecked = false; });
            buttons.Children.Add(all);
            buttons.Children.Add(none);
            bar.Children.Add(buttons);
            bar.Children.Add(searchGrid);
            Panel.Children.Add(bar);

            var list = new ListBox
            {
                ItemsSource = view,
                Height = height,
                ItemTemplate = (DataTemplate)XamlReader.Parse(CheckTemplate),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xD0, 0xD9)),
            };
            VirtualizingPanel.SetIsVirtualizing(list, true);
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            Panel.Children.Add(list);
            return collection;
        }

        public void Add(UIElement element) => Panel.Children.Add(element);

        // ------------------------------------------------------------------ leitura

        public bool Bool(string key) => Get(key) == "1";

        public int Index(string key) => int.TryParse(Get(key), out int v) ? v : -1;

        public string String(string key) => (Get(key) ?? string.Empty).Trim();

        public double Double(string key) => Conv.TryParse(Get(key), out double v) ? v : 0;

        public int Int(string key) => (int)Math.Round(Double(key));

        public List<T> Checked<T>(string key)
        {
            return _lists.TryGetValue(key, out ObservableCollection<CheckItem> c)
                ? c.Where(i => i.IsChecked).Select(i => i.Tag).OfType<T>().ToList()
                : new List<T>();
        }

        private string Get(string key) => _getters.TryGetValue(key, out Func<string> g) ? g() : null;

        /// <summary>Valida campos numéricos; retorna mensagem de erro ou null.</summary>
        public string Validate()
        {
            foreach (KeyValuePair<string, TextBox> kv in _numbers)
            {
                if (!Conv.TryParse(kv.Value.Text, out _))
                {
                    kv.Value.BorderBrush = Brushes.IndianRed;
                    kv.Value.Focus();
                    return "Informe um número válido no campo destacado.";
                }
                kv.Value.ClearValue(Control.BorderBrushProperty);
            }
            return null;
        }

        // ------------------------------------------------------------------ memória de valores

        public void LoadState()
        {
            foreach (KeyValuePair<string, Action<string>> kv in _setters)
            {
                string stored = SettingsService.GetUiValue(_stateId + "." + kv.Key);
                if (stored != null)
                {
                    try
                    {
                        kv.Value(stored);
                    }
                    catch
                    {
                        // valor antigo incompatível: ignora
                    }
                }
            }
        }

        public void SaveState()
        {
            var values = _getters.ToDictionary(kv => _stateId + "." + kv.Key, kv => kv.Value());
            SettingsService.SetUiValues(values);
        }

        public void RestoreDefaults()
        {
            foreach (KeyValuePair<string, string> kv in _defaults) _setters[kv.Key](kv.Value);
        }

        // ------------------------------------------------------------------ internos

        private void Register(string key, Func<string> getter, Action<string> setter)
        {
            _getters[key] = getter;
            _setters[key] = setter;
            _defaults[key] = getter();
        }

        private void AddRow(string label, FrameworkElement control, string tooltip)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.46, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.54, GridUnitType.Star) });
            var tb = new TextBlock { Text = label, Margin = new Thickness(0, 0, 10, 0), ToolTip = tooltip };
            Grid.SetColumn(control, 1);
            grid.Children.Add(tb);
            grid.Children.Add(control);
            Panel.Children.Add(grid);
        }

        private static void SelectText(ComboBox cb, string text)
        {
            if (text == null) return;
            foreach (object item in cb.Items)
            {
                if (string.Equals(item as string, text, StringComparison.OrdinalIgnoreCase))
                {
                    cb.SelectedItem = item;
                    return;
                }
            }
            if (cb.IsEditable) cb.Text = text;
        }

        private static Button MakeLink(string text, Action action)
        {
            var b = new Button { Content = text };
            b.SetResourceReference(FrameworkElement.StyleProperty, "LinkButton");
            b.Click += (s, e) => action();
            return b;
        }

        private const string CheckTemplate = @"
<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
  <CheckBox IsChecked='{Binding IsChecked, Mode=TwoWay}' Margin='2,1'>
    <StackPanel Orientation='Horizontal'>
      <TextBlock Text='{Binding Text}' TextWrapping='NoWrap'/>
      <TextBlock Text='{Binding Detail}' Foreground='#8A95A3' Margin='10,0,0,0' TextWrapping='NoWrap'/>
    </StackPanel>
  </CheckBox>
</DataTemplate>";
    }
}
