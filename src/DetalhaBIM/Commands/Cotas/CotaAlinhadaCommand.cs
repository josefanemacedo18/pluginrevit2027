using System.Linq;
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
    /// Cota alinhada com a parede em qualquer inclinação: clique na parede e no lado a cotar
    /// (vãos, paredes que chegam e total), ou meça livremente entre dois pontos paralelamente a
    /// uma parede, eixo ou linha. Repete até ESC.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CotaAlinhadaCommand : CommandBase
    {
        protected override string Title => "Cota Alinhada";

        private const ObjectSnapTypes Snaps = ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Midpoints
                                               | ObjectSnapTypes.Nearest | ObjectSnapTypes.Perpendicular | ObjectSnapTypes.Centers;

        protected override Result Run(CommandContext ctx)
        {
            ViewPlan view = Guards.RequirePlan(ctx);
            Document doc = ctx.Doc;
            CotasSettings s = ctx.Settings.Cotas;

            var dlg = new OptionsDialog("cota-alinhada", Title,
                "Cotas paralelas à parede em qualquer ângulo (paredes inclinadas, chanfros, planta rotacionada). Continue clicando; ESC encerra.",
                Theme.Cotas, ctx.MainWindow, "Começar");
            FormBuilder f = dlg.Form;
            f.Radio("modo", "Modo", new[]
            {
                "Parede: clique na parede e no lado a cotar",
                "Livre: dois pontos quaisquer, alinhada a uma parede, eixo ou linha",
            }, 0);
            f.Combo("tipo", "Tipo de cota", ProjectChoices.DimensionTypes(doc), Choices.Or(s.TipoCota, Choices.Default));
            f.Section("Modo parede");
            f.Check("vaos", "1ª linha: vãos de portas e janelas", true);
            f.Check("paredes", "2ª linha: paredes que chegam na face", true);
            f.Check("total", "3ª linha: comprimento total da face", true);
            f.Check("espessura", "Espessura da parede no ponto clicado", false);
            f.Number("primeira", "Da face até a 1ª linha", s.PrimeiraLinhaMm, "mm na folha");
            f.Number("espaco", "Entre linhas de cota", s.EspacamentoMm, "mm na folha");
            f.Hint("Nas pontas chanfradas o DetalhaBIM cria linhas invisíveis de referência exatamente nos cantos da face, para a cota medir o comprimento real.");
            if (!dlg.Run()) return Result.Cancelled;

            var builder = new DimensionBuilder(doc, view, Q.DimensionType(doc, Choices.Value(f.String("tipo"))), Conv.Cm(s.MenorSegmentoCm));
            var report = new Report(Title);
            var options = new AlignedOptions
            {
                Openings = f.Bool("vaos"),
                Walls = f.Bool("paredes"),
                Total = f.Bool("total"),
                Thickness = f.Bool("espessura"),
                FirstOffset = Conv.PaperMm(f.Double("primeira"), view),
                Spacing = Conv.PaperMm(f.Double("espaco"), view),
            };
            Guards.EnsureWorkPlane(doc, view);
            Selection sel = ctx.UiDoc.Selection;

            if (f.Index("modo") == 0)
            {
                var wallFilter = new PredicateFilter(e => e is Wall);
                while (true)
                {
                    Wall wall;
                    XYZ side;
                    try
                    {
                        wall = (Wall)doc.GetElement(sel.PickObject(ObjectType.Element, wallFilter, "Clique na parede (qualquer inclinação) — ESC encerra"));
                        side = sel.PickPoint(ObjectSnapTypes.None, "Clique do lado da face a cotar (onde as cotas ficarão)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }
                    Tx.Run(doc, Title, () =>
                    {
                        int n = new AlignedDimensionService(doc, view, builder, report).FromWall(wall, side, options);
                        report.Count("cotas alinhadas", n);
                        if (n == 0) report.Warn($"Nenhuma cota criada para a parede {wall.Id}.");
                    }, deleteFailingAnnotations: true);
                }
            }
            else
            {
                var lineFilter = new PredicateFilter(e => !AlignedDimensionService.DirectionOf(e).IsZeroLength());
                while (true)
                {
                    XYZ dir, p1, p2, pos;
                    try
                    {
                        Element refEl = doc.GetElement(sel.PickObject(ObjectType.Element, lineFilter,
                            "Clique na parede, eixo, plano de referência ou linha que define a direção — ESC encerra"));
                        dir = AlignedDimensionService.DirectionOf(refEl);
                        p1 = sel.PickPoint(Snaps, "Clique no 1º ponto a medir");
                        p2 = sel.PickPoint(Snaps, "Clique no 2º ponto a medir");
                        pos = sel.PickPoint(ObjectSnapTypes.None, "Clique onde a linha de cota deve passar");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }
                    Tx.Run(doc, Title, () =>
                    {
                        if (new AlignedDimensionService(doc, view, builder, report).BetweenPoints(dir, p1, p2, pos)) report.Count("cotas alinhadas");
                        else report.Warn("Uma das cotas livres não pôde ser criada.");
                    }, deleteFailingAnnotations: true);
                }
            }

            if (report.Warnings.Count > 0) report.Show();
            return Result.Succeeded;
        }
    }
}
