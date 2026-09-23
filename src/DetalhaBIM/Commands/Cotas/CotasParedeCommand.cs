using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Cotas
{
    /// <summary>
    /// Clique em uma parede e no lado externo: o plugin encontra as paredes alinhadas, os vãos
    /// e as paredes internas e cria as linhas de cota da fachada. Repete até pressionar ESC.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CotasParedeCommand : CommandBase
    {
        protected override string Title => "Cotas por Parede";

        protected override Result Run(CommandContext ctx)
        {
            ViewPlan view = Guards.RequirePlan(ctx);
            Document doc = ctx.Doc;
            CotasSettings s = ctx.Settings.Cotas;

            var dlg = new OptionsDialog("cotas-parede", Title,
                "Clique em uma parede e depois do lado onde as cotas devem ficar. O DetalhaBIM encontra sozinho as paredes alinhadas, os vãos e as paredes internas. Continue clicando em outras paredes; ESC encerra.",
                Theme.Cotas, ctx.MainWindow, "Começar");
            FormBuilder f = dlg.Form;
            f.Section("Linhas de cota");
            f.Combo("tipo", "Tipo de cota", Choices.DimensionTypes(doc), Choices.Or(s.TipoCota, Choices.Default));
            f.Check("vaos", "1ª linha: vãos de portas e janelas", true);
            f.Check("paredes", "2ª linha: paredes internas que chegam na fachada", true);
            f.Check("total", "3ª linha: cota total", true);
            f.Section("Afastamentos");
            f.Number("primeira", "Da face da parede até a 1ª linha", s.PrimeiraLinhaMm, "mm na folha");
            f.Number("espaco", "Entre linhas de cota", s.EspacamentoMm, "mm na folha");
            if (!dlg.Run()) return Result.Cancelled;

            var options = new FacadeOptions
            {
                Openings = f.Bool("vaos"),
                Walls = f.Bool("paredes"),
                Total = f.Bool("total"),
                FirstOffset = Conv.PaperMm(f.Double("primeira"), view),
                Spacing = Conv.PaperMm(f.Double("espaco"), view),
            };
            var builder = new DimensionBuilder(doc, view, Q.DimensionType(doc, Choices.Value(f.String("tipo"))), Conv.Cm(s.MenorSegmentoCm));
            var report = new Report(Title);
            Guards.EnsureWorkPlane(doc, view);

            var wallFilter = new PredicateFilter(e => e is Wall);
            while (true)
            {
                Reference picked;
                XYZ side;
                try
                {
                    picked = ctx.UiDoc.Selection.PickObject(ObjectType.Element, wallFilter, "Clique na parede da fachada (ESC para encerrar)");
                    side = ctx.UiDoc.Selection.PickPoint(ObjectSnapTypes.None, "Clique do lado em que as cotas serão posicionadas");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                var wall = (Wall)doc.GetElement(picked);
                Tx.Run(doc, Title, () =>
                {
                    var service = new FacadeDimensionService(doc, view, builder, report);
                    int n = service.DimensionFromWall(wall, side, options);
                    report.Count("cotas criadas", n);
                    if (n == 0) report.Warn("Nenhuma cota foi criada para uma das paredes selecionadas.");
                }, deleteFailingAnnotations: true);
            }

            if (report.Total > 0 || report.Warnings.Count > 0) report.Show();
            return Result.Succeeded;
        }
    }
}
