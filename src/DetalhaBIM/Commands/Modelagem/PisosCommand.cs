using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Modelagem
{
    /// <summary>Pisos de acabamento pelo contorno dos ambientes (com desnível para áreas molhadas).</summary>
    [Transaction(TransactionMode.Manual)]
    public class PisosCommand : CommandBase
    {
        protected override string Title => "Pisos por Ambiente";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            ModelagemSettings ms = ctx.Settings.Modelagem;
            List<string> types = Choices.Types<FloorType>(doc, false);
            if (types.Count == 0) throw new UserMessageException("O projeto não possui tipos de piso.");

            var dlg = new OptionsDialog("pisos", Title,
                "Cria o piso de acabamento de cada ambiente seguindo o contorno das paredes, com desnível opcional (ex.: -2 cm em áreas molhadas).",
                Theme.Modelagem, ctx.MainWindow, "Criar pisos");
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions, 0);
            f.Section("Piso");
            f.Combo("tipo", "Tipo de piso", types, Choices.Or(ms.TipoPiso, types[0]));
            f.Number("desnivel", "Desnível em relação ao nível", ms.DesnivelPisoCm, "cm");
            f.Check("pular", "Ignorar ambientes que já possuem um piso deste mesmo tipo", true);
            f.Check("nome", "Preencher \"Comentários\" do piso com o nome do ambiente", true);
            if (!dlg.Run()) return Result.Cancelled;

            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"));
            FloorType type = Q.TypeByLabel(Q.All<FloorType>(doc), f.String("tipo"));
            double offset = Conv.Cm(f.Double("desnivel"));
            double tol = ctx.UiApp.Application.ShortCurveTolerance;
            var report = new Report(Title);
            List<Floor> existing = f.Bool("pular")
                ? Q.All<Floor>(doc).Where(fl => fl.GetTypeId() == type.Id).ToList()
                : new List<Floor>();

            Tx.Run(doc, Title, () =>
            {
                foreach (Room room in rooms)
                {
                    if (existing.Any(fl => fl.LevelId == room.LevelId && Inside(room, fl)))
                    {
                        report.Count("ambientes que já tinham o piso (ignorados)");
                        continue;
                    }
                    List<CurveLoop> loops = RoomGeo.Loops(room, tol);
                    if (loops.Count == 0)
                    {
                        report.Warn($"Ambiente {RoomGeo.Label(room)}: contorno inválido.");
                        continue;
                    }
                    bool ok = Tx.TrySub(doc, () =>
                    {
                        Floor floor = Floor.Create(doc, loops, type.Id, room.LevelId);
                        Q.Set(floor, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM, offset + room.BaseOffset);
                        if (f.Bool("nome")) Q.Set(floor, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, RoomGeo.Label(room));
                    });
                    if (ok) report.Count("pisos criados");
                    else report.Warn($"Ambiente {RoomGeo.Label(room)}: o Revit não aceitou o contorno do piso.");
                }
            });

            report.Show();
            return Result.Succeeded;
        }

        private static bool Inside(Room room, Element e)
        {
            BoundingBoxXYZ bb = e.get_BoundingBox(null);
            return bb != null && RoomGeo.Contains(room, (bb.Min + bb.Max) / 2);
        }
    }
}
