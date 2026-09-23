using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Cotas
{
    /// <summary>Cotas externas automáticas de todo o pavimento (vãos, paredes e total por lado).</summary>
    [Transaction(TransactionMode.Manual)]
    public class CotasExternasCommand : CommandBase
    {
        protected override string Title => "Cotas Externas do Pavimento";

        protected override Result Run(CommandContext ctx)
        {
            ViewPlan view = Guards.RequirePlan(ctx);
            Document doc = ctx.Doc;
            CotasSettings s = ctx.Settings.Cotas;

            var dlg = new OptionsDialog("cotas-externas", Title,
                "Identifica automaticamente as paredes de fachada da planta ativa e cria as linhas de cota externas em cada lado da edificação.",
                Theme.Cotas, ctx.MainWindow);
            FormBuilder f = dlg.Form;
            f.Section("Lados da edificação");
            f.Check("inferior", "Inferior", true);
            f.Check("superior", "Superior", true);
            f.Check("esquerdo", "Esquerdo", true);
            f.Check("direito", "Direito", true);
            f.Section("Linhas de cota");
            f.Combo("tipo", "Tipo de cota", Choices.DimensionTypes(doc), Choices.Or(s.TipoCota, Choices.Default));
            f.Check("vaos", "1ª linha: vãos de portas e janelas", true);
            f.Check("paredes", "2ª linha: paredes internas", true);
            f.Check("total", "3ª linha: cota total", true);
            f.Section("Afastamentos");
            f.Number("primeira", "Da fachada até a 1ª linha", s.PrimeiraLinhaMm + 4, "mm na folha");
            f.Number("espaco", "Entre linhas de cota", s.EspacamentoMm, "mm na folha");
            f.Hint("Dica: a direção dos lados acompanha a orientação predominante das paredes, inclusive em edificações rotacionadas.");
            if (!dlg.Run()) return Result.Cancelled;

            var options = new FacadeOptions
            {
                Bottom = f.Bool("inferior"),
                Top = f.Bool("superior"),
                Left = f.Bool("esquerdo"),
                Right = f.Bool("direito"),
                Openings = f.Bool("vaos"),
                Walls = f.Bool("paredes"),
                Total = f.Bool("total"),
                FirstOffset = Conv.PaperMm(f.Double("primeira"), view),
                Spacing = Conv.PaperMm(f.Double("espaco"), view),
            };
            var builder = new DimensionBuilder(doc, view, Q.DimensionType(doc, Choices.Value(f.String("tipo"))), Conv.Cm(s.MenorSegmentoCm));
            var report = new Report(Title);

            Tx.Run(doc, Title, () =>
            {
                var service = new FacadeDimensionService(doc, view, builder, report);
                report.Count("cotas criadas", service.DimensionAllSides(options));
            }, deleteFailingAnnotations: true);

            report.Show();
            return Result.Succeeded;
        }
    }
}
