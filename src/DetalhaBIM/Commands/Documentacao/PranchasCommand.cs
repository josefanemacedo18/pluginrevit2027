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
    /// <summary>Cria pranchas em lote e distribui as vistas automaticamente dentro do carimbo.</summary>
    [Transaction(TransactionMode.Manual)]
    public class PranchasCommand : CommandBase
    {
        protected override string Title => "Criar Pranchas";

        private static readonly HashSet<ViewType> Placeable = new HashSet<ViewType>
        {
            ViewType.FloorPlan, ViewType.CeilingPlan, ViewType.EngineeringPlan, ViewType.AreaPlan, ViewType.Elevation,
            ViewType.Section, ViewType.ThreeD, ViewType.DraftingView, ViewType.Legend, ViewType.Detail, ViewType.Schedule,
        };

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            PranchasSettings ps = ctx.Settings.Pranchas;

            var placed = new HashSet<ElementId>(Q.All<Viewport>(doc).Select(v => v.ViewId));
            foreach (ScheduleSheetInstance s in Q.All<ScheduleSheetInstance>(doc)) placed.Add(s.ScheduleId);
            var preselected = new HashSet<ElementId>(ctx.UiDoc.Selection.GetElementIds());

            List<View> views = Q.All<View>(doc)
                .Where(v => !v.IsTemplate && Placeable.Contains(v.ViewType) && !(v is ViewSheet))
                .Where(v => v.ViewType == ViewType.Legend || !placed.Contains(v.Id))
                .Where(v => !(v is ViewSchedule vs) || (!vs.IsTitleblockRevisionSchedule && !vs.IsInternalKeynoteSchedule))
                .OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name, NaturalComparer.Instance).ToList();
            if (views.Count == 0) throw new UserMessageException("Todas as vistas do projeto já estão em pranchas.");

            var dlg = new OptionsDialog("pranchas", Title,
                "Escolha as vistas (apenas as que ainda não estão em pranchas aparecem) e o DetalhaBIM cria as pranchas, numera e organiza as vistas dentro da área útil do carimbo.",
                Theme.Documentacao, ctx.MainWindow, "Criar pranchas", 600);
            FormBuilder f = dlg.Form;
            f.Checklist("vistas", "Vistas", views.Select(v => new CheckItem(v.Name, v, preselected.Contains(v.Id), EtiquetarCommand.ViewTypeName(v))), 220);
            f.Hint("Dica: selecione vistas no Navegador de Projeto antes de abrir esta ferramenta para marcá-las automaticamente.");
            f.Section("Organização");
            f.Radio("modo", null, new[] { "Uma vista por prancha (nome da prancha = nome da vista)", "Agrupar as vistas em pranchas com arranjo automático" }, 0);
            f.Text("nome", "Nome das pranchas (modo agrupado)", "DETALHAMENTO");
            f.Section("Prancha");
            f.Combo("carimbo", "Carimbo (folha)", ProjectChoices.Symbols(doc, BuiltInCategory.OST_TitleBlocks, false), ps.Carimbo);
            f.Text("prefixo", "Prefixo da numeração", ps.PrefixoNumero);
            f.Number("digitos", "Dígitos da numeração", ps.Digitos);
            f.Number("faixa", "Faixa do carimbo à direita", ps.FaixaCarimboDireitaMm, "mm");
            f.Number("faixaInf", "Faixa do carimbo na base", ps.FaixaCarimboInferiorMm, "mm");
            if (!dlg.Run()) return Result.Cancelled;

            List<View> chosen = f.Checked<View>("vistas");
            if (chosen.Count == 0) throw new UserMessageException("Selecione ao menos uma vista.");

            var settings = new PranchasSettings
            {
                Carimbo = f.String("carimbo"),
                PrefixoNumero = f.String("prefixo"),
                Digitos = f.Int("digitos"),
                FaixaCarimboDireitaMm = f.Double("faixa"),
                FaixaCarimboInferiorMm = f.Double("faixaInf"),
                MargemMm = ps.MargemMm,
                EspacoEntreVistasMm = ps.EspacoEntreVistasMm,
            };
            var report = new Report(Title);
            var service = new SheetService(doc, report, settings);
            FamilySymbol tb = service.TitleBlock(settings.Carimbo);

            Tx.Run(doc, Title, () =>
            {
                if (f.Index("modo") == 0)
                {
                    foreach (View v in chosen) report.Count("pranchas criadas", service.Layout(new List<View> { v }, tb, v.Name).Count);
                }
                else
                {
                    report.Count("pranchas criadas", service.Layout(chosen, tb, f.String("nome")).Count);
                }
            });

            report.Count("vistas selecionadas", chosen.Count);
            report.Show();
            return Result.Succeeded;
        }
    }
}
