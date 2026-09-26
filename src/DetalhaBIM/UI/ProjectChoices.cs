using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DetalhaBIM.Core;

namespace DetalhaBIM.UI
{
    /// <summary>Listas de opções (tipos, modelos, famílias) lidas do projeto aberto no Revit.</summary>
    public static class ProjectChoices
    {
        public static List<string> DimensionTypes(Document doc) =>
            new[] { Choices.Default }.Concat(Q.LinearDimensionTypes(doc).Select(t => t.Name).Distinct()).ToList();

        public static List<string> SpotTypes(Document doc) =>
            new[] { Choices.Default }.Concat(Q.SpotElevationTypes(doc).Select(t => t.Name).Distinct()).ToList();

        public static List<string> Templates(Document doc, params ViewType[] types)
        {
            var set = new HashSet<ViewType>(types);
            return new[] { Choices.None }.Concat(new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => v.IsTemplate && (set.Count == 0 || set.Contains(v.ViewType)))
                .Select(v => v.Name).OrderBy(n => n)).ToList();
        }

        public static List<string> Symbols(Document doc, BuiltInCategory bic, bool withDefault = true)
        {
            IEnumerable<string> labels = Q.Symbols(doc, bic).Select(Q.Label);
            return (withDefault ? new[] { Choices.Default }.Concat(labels) : labels).ToList();
        }

        public static List<string> Materials(Document doc, string first) =>
            new[] { first }.Concat(Q.All<Material>(doc).Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n)).ToList();

        /// <summary>Tipos de perfil de parede (Wall Sweep) — rodapés, molduras, cimalhas.</summary>
        public static List<string> SweepTypes(Document doc) =>
            Q.SweepTypes(doc).Select(Q.Label).ToList();

        public static List<string> LineStyles(Document doc) =>
            RefLines.LineStyles(doc).Select(g => g.Name).ToList();

        public static List<string> Types<T>(Document doc, bool withDefault = true) where T : ElementType
        {
            IEnumerable<string> labels = Q.All<T>(doc).Select(Q.Label).OrderBy(n => n);
            return (withDefault ? new[] { Choices.Default }.Concat(labels) : labels).ToList();
        }
    }
}
