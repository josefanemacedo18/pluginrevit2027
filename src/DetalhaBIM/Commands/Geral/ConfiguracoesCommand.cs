using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Geral
{
    /// <summary>Abre as configurações do escritório.</summary>
    [Transaction(TransactionMode.Manual)]
    public class ConfiguracoesCommand : CommandBase
    {
        protected override string Title => "Configurações";

        protected override bool RequiresDocument => false;

        protected override Result Run(CommandContext ctx)
        {
            var window = new SettingsWindow(SettingsService.Clone(), Lists(ctx.Doc), ctx.MainWindow);
            if (window.ShowDialog() != true || window.Result == null) return Result.Cancelled;
            SettingsService.Save(window.Result);
            Dialogs.Info(Title, "Configurações salvas", "As novas configurações já valem para todas as ferramentas.");
            return Result.Succeeded;
        }

        private static ProjectLists Lists(Document doc)
        {
            var lists = new ProjectLists();
            if (doc == null || doc.IsFamilyDocument) return lists;

            List<string> Templates(params ViewType[] types) => Q.All<View>(doc)
                .Where(v => v.IsTemplate && types.Contains(v.ViewType)).Select(v => v.Name).OrderBy(n => n).ToList();

            lists.DimensionTypes = Q.LinearDimensionTypes(doc).Select(t => t.Name).Distinct().ToList();
            lists.SpotTypes = Q.SpotElevationTypes(doc).Select(t => t.Name).Distinct().ToList();
            lists.PlanTemplates = Templates(ViewType.FloorPlan);
            lists.CeilingTemplates = Templates(ViewType.CeilingPlan);
            lists.ElevationTemplates = Templates(ViewType.Elevation, ViewType.Section);
            lists.Templates3D = Templates(ViewType.ThreeD);
            lists.CeilingTypes = Q.All<CeilingType>(doc).Select(Q.Label).OrderBy(n => n).ToList();
            lists.FloorTypes = Q.All<FloorType>(doc).Select(Q.Label).OrderBy(n => n).ToList();
            lists.Lights = Q.Symbols(doc, BuiltInCategory.OST_LightingFixtures).Select(Q.Label).ToList();
            lists.TitleBlocks = Q.Symbols(doc, BuiltInCategory.OST_TitleBlocks).Select(Q.Label).ToList();
            return lists;
        }
    }

    /// <summary>Guia rápido das ferramentas e informações do plugin.</summary>
    [Transaction(TransactionMode.Manual)]
    public class SobreCommand : CommandBase
    {
        protected override string Title => "Ajuda";

        protected override bool RequiresDocument => false;

        protected override Result Run(CommandContext ctx)
        {
            new AboutWindow(ctx.MainWindow).ShowDialog();
            return Result.Succeeded;
        }
    }
}
