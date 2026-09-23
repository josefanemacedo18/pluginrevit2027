using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DetalhaBIM.UI
{
    /// <summary>
    /// Janela base do plugin: cabeçalho colorido com título e descrição, área de conteúdo
    /// rolável e rodapé com botões. Mantém o mesmo visual em todas as ferramentas.
    /// </summary>
    public class DetalhaWindow : Window
    {
        private readonly Border _body;
        private readonly StackPanel _footerLeft;
        private readonly StackPanel _footerRight;

        public DetalhaWindow(string title, string subtitle, Color accent, IntPtr owner, double width = 540)
        {
            Title = "DetalhaBIM - " + title;
            Width = width;
            MinWidth = 420;
            SizeToContent = SizeToContent.Height;
            MaxHeight = SystemParameters.WorkArea.Height * 0.92;
            WindowStartupLocation = owner != IntPtr.Zero ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Background = Brushes.White;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            Resources.MergedDictionaries.Add(Theme.Dictionary);
            Resources["Accent"] = Theme.Brush(accent);
            Accent = accent;

            if (owner != IntPtr.Zero) new WindowInteropHelper(this).Owner = owner;

            var root = new DockPanel { LastChildFill = true };

            // Cabeçalho
            var header = new Border
            {
                Background = new LinearGradientBrush(accent, Theme.Lighten(accent, 0.25), 0),
                Padding = new Thickness(22, 16, 22, 16),
            };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
            });
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                headerStack.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF)),
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                });
            }
            header.Child = headerStack;
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            // Rodapé
            var footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFA)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE1, 0xE5, 0xEB)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 10, 16, 10),
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _footerLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _footerRight = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(_footerRight, 1);
            footerGrid.Children.Add(_footerLeft);
            footerGrid.Children.Add(_footerRight);
            footer.Child = footerGrid;
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            _body = new Border { Padding = new Thickness(22, 8, 22, 18) };
            root.Children.Add(_body);

            Content = root;

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Close();
                }
            };
        }

        public Color Accent { get; }

        /// <summary>Define o conteúdo principal. Se <paramref name="scroll"/>, envolve em rolagem.</summary>
        protected void SetBody(UIElement content, bool scroll = true)
        {
            if (scroll)
            {
                _body.Child = new ScrollViewer
                {
                    Content = content,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Padding = new Thickness(0, 0, 4, 0),
                };
            }
            else
            {
                _body.Child = content;
            }
        }

        public Button AddButton(string text, RoutedEventHandler click, bool primary = false, bool left = false)
        {
            var b = new Button { Content = text };
            if (primary)
            {
                b.SetResourceReference(StyleProperty, "PrimaryButton");
                b.IsDefault = true;
            }
            else if (left)
            {
                b.SetResourceReference(StyleProperty, "LinkButton");
            }
            b.Click += click;
            (left ? _footerLeft : _footerRight).Children.Add(b);
            return b;
        }
    }
}
