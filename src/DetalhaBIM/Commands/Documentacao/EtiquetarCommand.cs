using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Documentacao
{
    /// <summary>Etiqueta ambientes, portas, janelas e outras categorias em várias vistas de uma vez.</summary>
    [Transaction(TransactionMode.Manual)]
    public class EtiquetarCommand : CommandBase
    {
        protected override string Title => "Etiquetar Vistas";

        private static readonly (string key, string label, BuiltInCategory cat, BuiltInCategory tag, bool on)[] Categories =
        {
            ("ambientes", "Ambientes", BuiltInCategory.OST_Rooms, BuiltInCategory.OST_RoomTags, true),
            ("portas", "Portas", BuiltInCategory.OST_Doors, BuiltInCategory.OST_DoorTags, true),
            ("janelas", "Janelas", BuiltInCategory.OST_Windows, BuiltInCategory.OST_WindowTags, true),
            ("mobiliario", "Mobiliário", BuiltInCategory.OST_Furniture, BuiltInCategory.OST_FurnitureTags, false),
            ("sanitarios", "Peças sanitárias", BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_PlumbingFixtureTags, false),
            ("luminarias", "Luminárias", BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingFixtureTags, false),
        };

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            ElementId active = ctx.ActiveView?.Id;
            List<View> candidates = Q.All<View>(doc)
                .Where(v => !v.IsTemplate && (v is ViewPlan || v is ViewSection))
                .OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name, NaturalComparer.Instance).ToList();

            var dlg = new OptionsDialog("etiquetar", Title,
                "Etiqueta automaticamente os elementos das vistas escolhidas, sem duplicar etiquetas existentes.",
                Theme.Documentacao, ctx.MainWindow, "Etiquetar", 580);
            FormBuilder f = dlg.Form;
            f.Checklist("vistas", "Vistas", candidates.Select(v => new CheckItem(v.Name, v, v.Id == active, ViewTypeName(v))), 170);
            f.Section("Categorias e tipos de etiqueta");
            foreach (var c in Categories)
            {
                f.Check(c.key, c.label, c.on);
                f.Combo(c.key + "Tipo", "   Etiqueta", Choices.Symbols(doc, c.tag), Choices.Default);
            }
            f.Section("Opções");
            f.Check("pular", "Ignorar elementos que já possuem etiqueta na vista", true);
            f.Number("afastamento", "Afastamento das etiquetas de portas e janelas", 4, "mm na folha");
            if (!dlg.Run()) return Result.Cancelled;

            List<View> views = f.Checked<View>("vistas");
            if (views.Count == 0) throw new UserMessageException("Selecione ao menos uma vista.");

            var report = new Report(Title);
            var service = new TagService(doc, report);
            bool skip = f.Bool("pular");
            double offset = f.Double("afastamento");

            using (var group = new TransactionGroup(doc, "DetalhaBIM - " + Title))
            {
                group.Start();
                foreach (View v in views)
                {
                    Tx.Run(doc, Title + " " + v.Name, () =>
                    {
                        foreach (var c in Categories)
                        {
                            if (!f.Bool(c.key)) continue;
                            FamilySymbol type = Q.SymbolByLabel(doc, c.tag, Choices.Value(f.String(c.key + "Tipo")));
                            if (type != null && !type.IsActive) type.Activate();
                            int n = c.cat == BuiltInCategory.OST_Rooms
                                ? service.TagRooms(v, type, skip)
                                : service.TagCategory(v, c.cat, type, skip, offset);
                            report.Count("etiquetas de " + c.label.ToLowerInvariant(), n);
                        }
                    }, deleteFailingAnnotations: true);
                }
                group.Assimilate();
            }

            report.Count("vistas processadas", views.Count);
            report.Show();
            return Result.Succeeded;
        }

        public static string ViewTypeName(View v)
        {
            switch (v.ViewType)
            {
                case ViewType.FloorPlan: return "Planta";
                case ViewType.CeilingPlan: return "Forro";
                case ViewType.EngineeringPlan: return "Planta estrutural";
                case ViewType.AreaPlan: return "Planta de área";
                case ViewType.Elevation: return "Elevação";
                case ViewType.Section: return "Corte";
                case ViewType.ThreeD: return "3D";
                case ViewType.DraftingView: return "Desenho";
                case ViewType.Legend: return "Legenda";
                case ViewType.Detail: return "Detalhe";
                case ViewType.Schedule: return "Tabela";
                default: return v.ViewType.ToString();
            }
        }
    }
}
