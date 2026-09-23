using System;
using System.Windows;
using System.Windows.Media;

namespace DetalhaBIM.UI
{
    /// <summary>
    /// Diálogo de opções padrão das ferramentas. Uso típico:
    /// <code>
    /// var dlg = new OptionsDialog("cotas-ambiente", "Cotas por Ambiente", "...", Theme.Cotas, ctx.MainWindow);
    /// dlg.Form.Check("aberturas", "Cotar vãos", true);
    /// if (!dlg.Run()) return Result.Cancelled;
    /// bool aberturas = dlg.Form.Bool("aberturas");
    /// </code>
    /// </summary>
    public class OptionsDialog : DetalhaWindow
    {
        private readonly string _executeText;
        private bool _built;

        public OptionsDialog(string id, string title, string subtitle, Color accent, IntPtr owner, string executeText = "Executar", double width = 540)
            : base(title, subtitle, accent, owner, width)
        {
            Form = new FormBuilder(id);
            _executeText = executeText;
        }

        public FormBuilder Form { get; }

        /// <summary>Exibe o diálogo (restaurando os últimos valores usados). Retorna true se confirmado.</summary>
        public bool Run()
        {
            if (!_built)
            {
                _built = true;
                SetBody(Form.Panel);
                AddButton("Restaurar padrões", (s, e) => Form.RestoreDefaults(), left: true);
                AddButton("Cancelar", (s, e) => { DialogResult = false; });
                AddButton(_executeText, (s, e) => Confirm(), primary: true);
                Form.LoadState();
            }
            return ShowDialog() == true;
        }

        private void Confirm()
        {
            string error = Form.Validate();
            if (error != null)
            {
                MessageBox.Show(this, error, "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Form.SaveState();
            DialogResult = true;
        }
    }
}
