using System.Collections.Generic;

namespace DetalhaBIM.UI
{
    /// <summary>
    /// Constantes e conversões das listas de opções dos diálogos. Não depende do Revit, para que as
    /// janelas possam ser testadas isoladamente (as listas vindas do projeto ficam em ProjectChoices).
    /// </summary>
    public static class Choices
    {
        public const string Default = "<Padrão do projeto>";
        public const string None = "<Nenhum>";

        /// <summary>Converte a escolha do diálogo em nome (vazio = padrão/nenhum).</summary>
        public static string Value(string choice) => choice == Default || choice == None ? string.Empty : choice ?? string.Empty;

        public static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

        public static readonly List<string> Scales = new List<string> { "1:10", "1:20", "1:25", "1:50", "1:75", "1:100", "1:125", "1:200", "1:250", "1:500" };

        public static string Scale(int scale) => "1:" + scale;

        public static int ParseScale(string text, int fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            string t = text.Contains(":") ? text.Substring(text.IndexOf(':') + 1) : text;
            return int.TryParse(t.Trim(), out int v) && v > 0 ? v : fallback;
        }
    }
}
