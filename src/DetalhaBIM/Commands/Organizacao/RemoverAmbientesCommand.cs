using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Organizacao
{
    /// <summary>Localiza e exclui definitivamente ambientes não colocados, não delimitados ou redundantes.</summary>
    [Transaction(TransactionMode.Manual)]
    public class RemoverAmbientesCommand : CommandBase
    {
        protected override string Title => "Limpar Ambientes";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            var notEnclosed = new HashSet<ElementId>();
            var redundant = new HashSet<ElementId>();
            Guid notEnclosedId = BuiltInFailures.RoomFailures.RoomNotEnclosed.Guid;
            Guid redundantId = BuiltInFailures.RoomFailures.RoomsInSameRegionRooms.Guid;
            Guid sameRegionId = BuiltInFailures.RoomFailures.RoomsInSameRegion.Guid;
            foreach (FailureMessage w in doc.GetWarnings())
            {
                Guid g = w.GetFailureDefinitionId().Guid;
                if (g == notEnclosedId) foreach (ElementId id in w.GetFailingElements()) notEnclosed.Add(id);
                if (g == redundantId || g == sameRegionId) foreach (ElementId id in w.GetFailingElements()) redundant.Add(id);
            }

            List<Room> rooms = Q.Rooms(doc);
            if (rooms.Count == 0) throw new UserMessageException("O projeto não possui ambientes.");

            var rows = rooms.Select(r =>
            {
                string status;
                bool problem = true;
                if (r.Location == null) status = "Não colocado";
                else if (redundant.Contains(r.Id) && r.Area <= Geo.Eps) status = "Redundante";
                else if (notEnclosed.Contains(r.Id) || r.Area <= Geo.Eps) status = "Não delimitado";
                else
                {
                    status = "OK";
                    problem = false;
                }
                return new RoomRow
                {
                    Id = r.Id.Value,
                    Number = RoomGeo.Number(r),
                    Name = RoomGeo.Name(r),
                    Level = r.Level?.Name ?? "—",
                    Status = status,
                    Area = r.Area > Geo.Eps ? Conv.Format(Conv.ToM2(r.Area)) : "—",
                    IsProblem = problem,
                    Selected = problem,
                };
            }).OrderByDescending(r => r.IsProblem).ThenBy(r => r.Number, NaturalComparer.Instance).ToList();

            var window = new RoomCleanupWindow(rows, ctx.MainWindow);
            if (window.ShowDialog() != true) return Result.Cancelled;

            List<ElementId> ids = window.SelectedIds.Select(v => new ElementId(v)).ToList();
            var report = new Report(Title);
            Tx.Run(doc, Title, () =>
            {
                ICollection<ElementId> deleted = doc.Delete(ids);
                report.Count("ambientes excluídos", ids.Count);
                int extra = deleted.Count - ids.Count;
                if (extra > 0) report.Count("etiquetas e elementos dependentes removidos", extra);
            });
            report.Show();
            return Result.Succeeded;
        }
    }
}
