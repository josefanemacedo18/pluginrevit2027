using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.UI;

namespace DetalhaBIM.Core
{
    /// <summary>
    /// Acumula o resultado de um processamento em lote (itens criados e avisos) e exibe um
    /// resumo claro ao final, em vez de interromper o usuário a cada problema.
    /// </summary>
    public class Report
    {
        private readonly string _title;
        private readonly Dictionary<string, int> _counters = new Dictionary<string, int>();
        private readonly List<string> _order = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        public Report(string title)
        {
            _title = title;
        }

        public IReadOnlyList<string> Warnings => _warnings;

        public int Total => _counters.Values.Sum();

        public void Count(string what, int amount = 1)
        {
            if (!_counters.ContainsKey(what))
            {
                _counters[what] = 0;
                _order.Add(what);
            }
            _counters[what] += amount;
        }

        public int Get(string what) => _counters.TryGetValue(what, out int v) ? v : 0;

        public void Warn(string message)
        {
            if (!_warnings.Contains(message)) _warnings.Add(message);
            Logger.Warn(_title + ": " + message);
        }

        public void Show(string emptyMessage = "Nenhum elemento foi criado ou alterado.")
        {
            var content = new StringBuilder();
            foreach (string key in _order)
            {
                if (_counters[key] > 0) content.AppendLine($"• {_counters[key]} {key}");
            }

            var td = new TaskDialog("DetalhaBIM")
            {
                Title = "DetalhaBIM - " + _title,
                MainInstruction = Total > 0 ? "Concluído" : emptyMessage,
                MainContent = content.ToString().TrimEnd(),
                CommonButtons = TaskDialogCommonButtons.Ok,
            };

            if (_warnings.Count > 0)
            {
                td.MainIcon = Total > 0 ? TaskDialogIcon.TaskDialogIconInformation : TaskDialogIcon.TaskDialogIconWarning;
                td.FooterText = $"{_warnings.Count} aviso(s) — expanda para ver os detalhes.";
                td.ExpandedContent = string.Join("\n", _warnings.Take(60).Select(w => "• " + w))
                                     + (_warnings.Count > 60 ? $"\n... e mais {_warnings.Count - 60}." : string.Empty);
            }
            td.Show();
        }
    }
}
