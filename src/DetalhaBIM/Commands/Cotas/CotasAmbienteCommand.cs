using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Cotas
{
    /// <summary>Cotas internas automáticas por ambiente (face a face + vãos de portas e janelas).</summary>
    [Transaction(TransactionMode.Manual)]
    public class CotasAmbienteCommand : CommandBase
    {
        protected override string Title => "Cotas por Ambiente";

        protected override Result Run(CommandContext ctx)
        {
            ViewPlan view = Guards.RequirePlan(ctx);
            Document doc = ctx.Doc;
            CotasSettings s = ctx.Settings.Cotas;

            var dlg = new OptionsDialog("cotas-ambiente", Title,
                "Cria cadeias de cotas internas face a face em cada ambiente, nas duas direções, e opcionalmente os vãos de portas e janelas de cada parede.",
                Theme.Cotas, ctx.MainWindow);
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions.Take(3).ToList(), 0);
            f.Section("Cotas");
            f.Combo("tipo", "Tipo de cota", ProjectChoices.DimensionTypes(doc), Choices.Or(s.TipoCota, Choices.Default));
            f.Check("horizontal", "Cota horizontal (largura do ambiente)", true);
            f.Check("vertical", "Cota vertical (profundidade do ambiente)", true);
            f.Check("aberturas", "Cotar vãos de portas e janelas ao longo das paredes", true);
            f.Section("Posição das linhas de cota");
            f.Radio("posicao", null, new[] { "No centro do ambiente", "Junto à parede (inferior/esquerda)" }, 0);
            f.Number("afastamento", "Afastamento da parede", s.AfastamentoInternoMm, "mm na folha");
            f.Hint("Distâncias em \"mm na folha\" se ajustam à escala da vista (ex.: 6 mm em 1:50 = 30 cm no modelo).");
            if (!dlg.Run()) return Result.Cancelled;

            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"));
            var report = new Report(Title);
            List<Room> onLevel = rooms.Where(r => r.LevelId == view.GenLevel.Id).ToList();
            if (onLevel.Count < rooms.Count)
                report.Warn($"{rooms.Count - onLevel.Count} ambiente(s) de outros níveis foram ignorados (cotas só podem ser criadas na planta do próprio nível).");

            double offset = Conv.PaperMm(f.Double("afastamento"), view);
            var options = new RoomDimensionOptions
            {
                Horizontal = f.Bool("horizontal"),
                Vertical = f.Bool("vertical"),
                AtCenter = f.Index("posicao") == 0,
                WallOffset = offset,
                Openings = f.Bool("aberturas"),
                OpeningsOffset = offset,
            };
            var builder = new DimensionBuilder(doc, view, Q.DimensionType(doc, Choices.Value(f.String("tipo"))), Conv.Cm(s.MenorSegmentoCm));

            Tx.Run(doc, Title, () =>
            {
                var service = new RoomDimensionService(doc, view, builder, report);
                foreach (Room room in onLevel)
                {
                    report.Count("cotas criadas", service.Dimension(room, options));
                }
            }, deleteFailingAnnotations: true);

            report.Count("ambientes processados", onLevel.Count);
            report.Show();
            return Result.Succeeded;
        }
    }
}
