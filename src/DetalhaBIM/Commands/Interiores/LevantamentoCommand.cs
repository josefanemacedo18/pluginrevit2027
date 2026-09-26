using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Interiores
{
    /// <summary>Levantamento de acabamentos por ambiente, com exportação para Excel.</summary>
    [Transaction(TransactionMode.Manual)]
    public class LevantamentoCommand : CommandBase
    {
        protected override string Title => "Levantamento de Acabamentos";

        protected override Result Run(CommandContext ctx)
        {
            var dlg = new OptionsDialog("levantamento", Title,
                "Calcula por ambiente: piso, perímetro, paredes (descontando portas e janelas), teto e rodapé (descontando portas), com perda para compra. Nada é alterado no modelo.",
                Theme.Interiores, ctx.MainWindow, "Calcular");
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions, 3);
            f.Section("Cálculo");
            f.Check("forro", "Altura das paredes até o forro (quando houver forro sobre o ambiente)", true);
            f.Number("perdaPiso", "Perda do piso", 10, "%");
            f.Number("perdaParede", "Perda das paredes (revestimento/pintura)", 10, "%");
            if (!dlg.Run()) return Result.Cancelled;

            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"));
            var service = new QuantityService(ctx.Doc);
            double lf = f.Double("perdaPiso") / 100, lw = f.Double("perdaParede") / 100;
            List<string[]> rows = rooms.Select(r => QuantityService.Row(service.Measure(r, f.Bool("forro")), lf, lw)).ToList();
            rows.Add(QuantityService.Total(rows));

            new TableWindow(Title,
                $"{rooms.Count} ambiente(s). Paredes = comprimento das paredes do contorno × pé-direito, menos os vãos. Rodapé = contorno com paredes menos as portas.",
                Theme.Interiores, ctx.MainWindow, QuantityService.Headers, rows, "levantamento-acabamentos.csv",
                "Linhas separadoras de ambiente não contam como parede. Vãos em paredes de vínculos não são descontados.").ShowDialog();
            return Result.Succeeded;
        }
    }
}
