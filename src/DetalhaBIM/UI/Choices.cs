using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DetalhaBIM.Core;

namespace DetalhaBIM.UI
{
    /// <summary>Listas de opções (tipos, modelos) para os diálogos, a partir do projeto aberto.</summary>
    public static class Choices
    {
        public const string Default = "<Padrão do projeto>";
        public const string None = "<Nenhum>";

        /// <summary>Converte a escolha do diálogo em nome (vazio = padrão/nenhum).</summary>
        public static string Value(string choice) => choice == Default || choice == None ? string.Empty : choice ?? string.Empty;

        public static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

        public static List<string> DimensionTypes(Document doc) =>
            new[] { Default }.Concat(Q.LinearDimensionTypes(doc).Select(t => t.Name).Distinct()).ToList();

        public static List<string> SpotTypes(Document doc) =>
            new[] { Default }.Concat(Q.SpotElevationTypes(doc).Select(t => t.Name).Distinct()).ToList();

        public static List<string> Templates(Document doc, params ViewType[] types)
        {
            var set = new HashSet<ViewType>(types);
            return new[] { None }.Concat(new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => v.IsTemplate && (set.Count == 0 || set.Contains(v.ViewType)))
                .Select(v => v.Name).OrderBy(n => n)).ToList();
        }

        public static List<string> Symbols(Document doc, BuiltInCategory bic, bool withDefault = true)
        {
            IEnumerable<string> labels = Q.Symbols(doc, bic).Select(Q.Label);
            return (withDefault ? new[] { Default }.Concat(labels) : labels).ToList();
        }

        public static List<string> Types<T>(Document doc, bool withDefault = true) where T : ElementType
        {
            IEnumerable<string> labels = Q.All<T>(doc).Select(Q.Label).OrderBy(n => n);
            return (withDefault ? new[] { Default }.Concat(labels) : labels).ToList();
        }

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
