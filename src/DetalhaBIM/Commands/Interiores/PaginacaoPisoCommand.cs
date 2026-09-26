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
    /// <summary>
    /// Paginação de piso modelada: o piso de revestimento de cada ambiente é criado com o
    /// tipo/material da placa e dividido em placas reais nas juntas, com quantitativo.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PaginacaoPisoCommand : CommandBase
    {
        protected override string Title => "Paginação de Piso";

        private const string Auto = "<Criar pela descrição, espessura e material>";

        protected override Result Run(CommandContext ctx)
        {
            ViewPlan view = Guards.RequirePlan(ctx);
            Document doc = ctx.Doc;
            List<string> floorTypes = new[] { Auto }.Concat(Q.All<FloorType>(doc).Where(t => !t.IsFoundationSlab).Select(Q.Label).OrderBy(n => n)).ToList();

            var dlg = new OptionsDialog("paginacao-piso", Title,
                "Modela o piso de revestimento de cada ambiente, placa por placa: as placas inteiras e as cortadas seguem o contorno (contornando pilares) e a junta é um vão real entre elas. Aparece na planta, no 3D, nos cortes e nas tabelas.",
                Theme.Interiores, ctx.MainWindow, "Paginar");
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions.Take(3).ToList(), 0);
            f.Section("Placa");
            f.Text("descricao", "Descrição", "Porcelanato");
            f.Number("largura", "Largura", 60, "cm");
            f.Number("comprimento", "Comprimento", 60, "cm");
            f.Number("junta", "Junta (rejunte)", 2, "mm");
            f.Number("espessura", "Espessura do revestimento", 1, "cm");
            f.Combo("material", "Material (digite para criar)", ProjectChoices.Materials(doc, Choices.Default), "Porcelanato", editable: true);
            f.Combo("tipo", "Tipo de piso", floorTypes, Auto);
            f.Section("Paginação");
            f.Radio("inicio", "Ponto de partida", new[]
            {
                "Junta centralizada no ambiente",
                "Placa centralizada no ambiente",
                "Placa inteira no canto do ambiente",
                "Clicar um ponto (canto de uma placa) — alinhado entre os ambientes",
            }, 0);
            f.Combo("angulo", "Ângulo em relação à maior parede", new[] { "0", "45", "90" }, "0", editable: true, tooltip: "Graus. 45 = paginação diagonal.");
            f.Section("Modelagem");
            f.Radio("metodo", null, new[]
            {
                "Um piso por ambiente dividido nas juntas (peças do Revit) — recomendado, leve",
                "Um piso separado para cada placa (até 2.500 placas por ambiente)",
            }, 0);
            f.Check("sobre", "Apoiar sobre o piso existente do ambiente (laje/contrapiso)", true);
            f.Number("desloc", "Deslocamento adicional da base", 0, "cm");
            f.Check("mostrar", "Mostrar as placas nesta planta, nas plantas do mesmo nível e nas vistas 3D", true,
                "Ajusta \"Visibilidade de peças\" = Mostrar peças. Vistas controladas por modelo não são alteradas.");
            f.Check("substituir", "Substituir a paginação anterior do ambiente (inclusive as linhas da versão 1.3)", true);
            f.Check("nota", "Nota com a quantidade de placas no ambiente", false);
            f.Section("Quantitativo");
            f.Number("perda", "Perda (recortes e quebras)", 10, "%");
            f.Number("caixa", "m² por caixa (0 = não calcular)", 0, "m²");
            if (!dlg.Run()) return Result.Cancelled;

            double w = Conv.Cm(f.Double("largura")), l = Conv.Cm(f.Double("comprimento"));
            double thickness = Conv.Cm(f.Double("espessura"));
            if (w < Conv.Cm(1) || l < Conv.Cm(1)) throw new UserMessageException("Informe as dimensões da placa (mínimo 1 cm).");
            if (thickness < Conv.Mm(1)) throw new UserMessageException("Informe a espessura do revestimento (mínimo 1 mm).");
            if (!Conv.TryParse(f.String("angulo"), out double angle)) angle = 0;

            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"));
            var report = new Report(Title);
            int other = rooms.Count(r => r.LevelId != view.GenLevel.Id);
            if (other > 0) report.Warn($"{other} ambiente(s) de outros níveis foram ignorados (use a planta do nível deles).");
            rooms = rooms.Where(r => r.LevelId == view.GenLevel.Id).ToList();
            if (rooms.Count == 0) throw new UserMessageException("Nenhum ambiente do nível desta planta foi selecionado.");

            XYZ point = null;
            if (f.Index("inicio") == 3)
            {
                Guards.EnsureWorkPlane(doc, view);
                point = ctx.UiDoc.Selection.PickPoint(ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Nearest,
                    "Clique no ponto de partida da paginação (canto de uma placa)");
            }

            var o = new TileFloorOptions
            {
                Method = f.Index("metodo"),
                Width = w,
                Length = l,
                Joint = Conv.Mm(f.Double("junta")),
                Start = f.Index("inicio"),
                Point = point,
                AngleDeg = angle,
                OnExisting = f.Bool("sobre"),
                Offset = Conv.Cm(f.Double("desloc")),
                Replace = f.Bool("substituir"),
                Note = f.Bool("nota"),
                Description = f.String("descricao"),
                Loss = f.Double("perda") / 100,
                BoxM2 = f.Double("caixa"),
            };

            var rows = new List<string[]>();
            var service = new TileFloorService(doc, view, report, ctx.UiApp.Application.ShortCurveTolerance);
            Tx.Run(doc, Title, () =>
            {
                FloorType type = f.String("tipo") == Auto ? null : Q.TypeByLabel(Q.All<FloorType>(doc), f.String("tipo"));
                if (type == null)
                {
                    Material mat = Q.Material(doc, Choices.Value(f.String("material")), true, report, new Color(214, 206, 192));
                    type = TileFloorService.EnsureType(doc, o.Description, w, l, thickness, mat);
                }
                o.Type = type;
                foreach (Room room in rooms)
                {
                    string[] row = service.Layout(room, o);
                    if (row != null) rows.Add(row);
                }

                if (f.Bool("mostrar") && service.PartsCreated)
                {
                    var views = new List<View> { view };
                    views.AddRange(Q.All<ViewPlan>(doc).Where(v => !v.IsTemplate && v.GenLevel?.Id == view.GenLevel.Id && v.Id != view.Id));
                    views.AddRange(Q.All<View3D>(doc).Where(v => !v.IsTemplate));
                    int n = TileFloorService.ShowParts(views);
                    if (n > 0) report.Count("vistas passaram a mostrar as placas (Visibilidade de peças)", n);
                }
            });

            report.Show();
            if (rows.Count > 0)
            {
                rows.Add(Total(rows));
                new TableWindow(Title, "Quantitativo de placas por ambiente. Perda aplicada sobre a área; placas cortadas contam como placas compradas.",
                    Theme.Interiores, ctx.MainWindow, TileFloorService.Headers, rows, "paginacao-piso.csv",
                    "Dica: as placas são peças (Parts) do piso — use uma tabela de Peças para contar por material, ou selecione uma placa para trocar o material.").ShowDialog();
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
                ((int)Sum(6)).ToString(), Conv.Format(Sum(7), 2), boxes, string.Empty,
            };
        }
    }
}
