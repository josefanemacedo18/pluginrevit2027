using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Interiores
{
    /// <summary>Locação de mobiliário, marcenaria e pontos até as paredes (planta) e alturas (elevação).</summary>
    [Transaction(TransactionMode.Manual)]
    public class LocacaoCommand : CommandBase
    {
        protected override string Title => "Locação de Objetos";

        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_Furniture, BuiltInCategory.OST_FurnitureSystems, BuiltInCategory.OST_Casework,
            BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_CommunicationDevices,
            BuiltInCategory.OST_DataDevices, BuiltInCategory.OST_SpecialityEquipment, BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_Entourage, BuiltInCategory.OST_Planting,
        };

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            View view = ctx.ActiveView;
            if (!LocationDimensionService.Supports(view))
                throw new UserMessageException("Abra uma planta (locação até as paredes) ou uma elevação/corte (locação e alturas) e tente novamente.");
            CotasSettings s = ctx.Settings.Cotas;

            var dlg = new OptionsDialog("locacao", Title,
                "Selecione móveis, marcenaria, louças, tomadas, interruptores ou luminárias: o DetalhaBIM cota cada um até as paredes mais próximas e, nas elevações, a altura em relação ao piso acabado.",
                Theme.Interiores, ctx.MainWindow, "Selecionar");
            FormBuilder f = dlg.Form;
            f.Section("Referência do objeto");
            f.Radio("ref", null, new[]
            {
                "Faces do objeto (mobiliário e marcenaria)",
                "Eixo/centro do objeto (pontos elétricos, hidráulicos e luminárias)",
            }, 0);
            f.Check("tamanho", "Incluir a dimensão do próprio objeto na cadeia", true);
            f.Section("Direções");
            f.Check("ambos", "Até as paredes dos dois lados (desmarcado: só a mais próxima)", false);
            f.Check("x", "Planta: ao longo da largura do objeto", true);
            f.Check("y", "Planta: ao longo da profundidade do objeto", true);
            f.Check("alturas", "Elevação/corte: altura em relação ao piso acabado", true);
            f.Number("afastamento", "Deslocamento da linha de cota", 0, "mm na folha", "0 = a linha passa pelo centro do objeto.");
            f.Combo("tipo", "Tipo de cota", ProjectChoices.DimensionTypes(doc), Choices.Or(s.TipoCota, Choices.Default));
            if (!dlg.Run()) return Result.Cancelled;

            List<Element> elements = Pick.SelectedOrPick(ctx.UiDoc, PredicateFilter.Categories(Categories),
                "Selecione os objetos a locar e clique em Concluir");
            if (elements.Count == 0) return Result.Cancelled;

            var o = new LocationOptions
            {
                Center = f.Index("ref") == 1,
                IncludeSize = f.Bool("tamanho"),
                BothSides = f.Bool("ambos"),
                AxisX = f.Bool("x"),
                AxisY = f.Bool("y"),
                Heights = f.Bool("alturas"),
                Offset = Conv.PaperMm(f.Double("afastamento"), view),
            };
            var report = new Report(Title);
            var builder = new DimensionBuilder(doc, view, Q.DimensionType(doc, Choices.Value(f.String("tipo"))), Conv.Cm(s.MenorSegmentoCm));
            Tx.Run(doc, Title, () =>
            {
                var service = new LocationDimensionService(doc, view, builder, report);
                foreach (Element e in elements)
                {
                    int n = service.Locate(e, o);
                    report.Count("cotas de locação", n);
                    if (n > 0) report.Count("objetos locados");
                }
            }, deleteFailingAnnotations: true);

            if (report.Warnings.Count > 0 || report.Get("cotas de locação") == 0) report.Show();
            return Result.Succeeded;
        }
    }
}
