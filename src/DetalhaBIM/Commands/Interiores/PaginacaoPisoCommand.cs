using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Interiores
{
    /// <summary>Paginação de piso por ambiente: juntas desenhadas e quantitativo de peças.</summary>
    [Transaction(TransactionMode.Manual)]
    public class PaginacaoPisoCommand : CommandBase
    {
        protected override string Title => "Paginação de Piso";

        protected override Result Run(CommandContext ctx)
        {
            ViewPlan view = Guards.RequirePlan(ctx);
            Document doc = ctx.Doc;
            List<string> styles = ProjectChoices.LineStyles(doc);
            string thin = styles.FirstOrDefault(n => n.Equals("Linhas finas", System.StringComparison.OrdinalIgnoreCase) || n.Equals("Thin Lines", System.StringComparison.OrdinalIgnoreCase))
                          ?? styles.FirstOrDefault() ?? Choices.Default;

            var dlg = new OptionsDialog("paginacao-piso", Title,
                "Desenha as juntas do revestimento dentro de cada ambiente (contornando pilares) e calcula peças inteiras, cortadas, área com perda e caixas.",
                Theme.Interiores, ctx.MainWindow, "Paginar");
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions.Take(3).ToList(), 0);
            f.Section("Peça");
            f.Text("descricao", "Descrição (vai na nota)", "PORCELANATO");
            f.Number("largura", "Largura da peça", 60, "cm");
            f.Number("comprimento", "Comprimento da peça", 60, "cm");
            f.Number("junta", "Junta (rejunte)", 2, "mm");
            f.Section("Paginação");
            f.Radio("inicio", "Ponto de partida", new[]
            {
                "Junta centralizada no ambiente",
                "Peça centralizada no ambiente",
                "Peça inteira no canto do ambiente",
                "Clicar um ponto (canto de uma peça) — mesmo alinhamento para todos",
            }, 0);
            f.Combo("angulo", "Ângulo em relação à maior parede", new[] { "0", "45", "90" }, "0", editable: true, tooltip: "Graus. 45 = paginação diagonal.");
            f.Section("Desenho e quantitativo");
            f.Check("linhas", "Desenhar as juntas (linhas de detalhe)", true);
            f.Combo("estilo", "Estilo de linha", styles.Count > 0 ? styles : new List<string> { Choices.Default }, thin);
            f.Check("grupo", "Agrupar a paginação de cada ambiente (fácil de mover/apagar)", true);
            f.Check("nota", "Nota com a quantidade de peças no ambiente", true);
            f.Check("substituir", "Substituir a paginação anterior do ambiente nesta vista", true);
            f.Number("perda", "Perda (recortes e quebras)", 10, "%");
            f.Number("caixa", "m² por caixa (0 = não calcular)", 0, "m²");
            if (!dlg.Run()) return Result.Cancelled;

            double w = Conv.Cm(f.Double("largura")), l = Conv.Cm(f.Double("comprimento"));
            if (w < Conv.Cm(1) || l < Conv.Cm(1)) throw new UserMessageException("Informe as dimensões da peça (mínimo 1 cm).");
            if (!Conv.TryParse(f.String("angulo"), out double angle)) angle = 0;

            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"));
            var report = new Report(Title);
            List<Room> other = rooms.Where(r => r.LevelId != view.GenLevel.Id).ToList();
            if (other.Count > 0) report.Warn($"{other.Count} ambiente(s) de outros níveis foram ignorados (a paginação é desenhada na planta ativa).");
            rooms = rooms.Where(r => r.LevelId == view.GenLevel.Id).ToList();
            if (rooms.Count == 0) throw new UserMessageException("Nenhum ambiente do nível desta planta foi selecionado.");

            XYZ point = null;
            if (f.Index("inicio") == 3)
            {
                Guards.EnsureWorkPlane(doc, view);
                point = ctx.UiDoc.Selection.PickPoint(ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Nearest,
                    "Clique no ponto de partida da paginação (canto de uma peça)");
            }

            var o = new TileOptions
            {
                Width = w,
                Length = l,
                Joint = Conv.Mm(f.Double("junta")),
                Start = f.Index("inicio"),
                Point = point,
                AngleDeg = angle,
                DrawLines = f.Bool("linhas"),
                Style = RefLines.LineStyles(doc).FirstOrDefault(g => g.Name == f.String("estilo")),
                Group = f.Bool("grupo"),
                Note = f.Bool("nota"),
                Replace = f.Bool("substituir"),
                Description = f.String("descricao"),
                Loss = f.Double("perda") / 100,
                BoxM2 = f.Double("caixa"),
            };

            var rows = new List<string[]>();
            var service = new TileLayoutService(doc, view, report);
            Tx.Run(doc, Title, () =>
            {
                foreach (Room room in rooms)
                {
                    string[] row = service.Layout(room, o);
                    if (row != null) rows.Add(row);
                }
            });

            if (report.Warnings.Count > 0) report.Show();
            if (rows.Count > 0)
            {
                rows.Add(Total(rows));
                new TableWindow(Title, "Quantitativo de peças por ambiente. Perda aplicada sobre a área; peças cortadas contam como peças inteiras compradas.",
                    Theme.Interiores, ctx.MainWindow, TileLayoutService.Headers, rows, "paginacao-piso.csv").ShowDialog();
            }
            return Result.Succeeded;
        }

        private static string[] Total(List<string[]> rows)
        {
            double Sum(int i) => rows.Sum(r => Conv.TryParse(r[i], out double v) ? v : 0);
            string boxes = rows.All(r => r[8] == "-") ? "-" : ((int)Sum(8)).ToString();
            return new[]
            {
                "TOTAL", Conv.Format(Sum(1), 2), string.Empty, string.Empty, ((int)Sum(4)).ToString(), ((int)Sum(5)).ToString(),
                ((int)Sum(6)).ToString(), Conv.Format(Sum(7), 2), boxes,
            };
        }
    }
}
