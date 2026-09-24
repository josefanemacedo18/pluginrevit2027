using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Cotas
{
    /// <summary>Cota de nível (spot elevation) sobre o piso de cada ambiente.</summary>
    [Transaction(TransactionMode.Manual)]
    public class NiveisAmbienteCommand : CommandBase
    {
        protected override string Title => "Níveis de Piso por Ambiente";

        protected override Result Run(CommandContext ctx)
        {
            ViewPlan view = Guards.RequirePlan(ctx);
            Document doc = ctx.Doc;

            var dlg = new OptionsDialog("niveis-ambiente", Title,
                "Insere a cota de nível do piso acabado em cada ambiente (ex.: +0,00 / -0,02), lida diretamente do piso modelado.",
                Theme.Cotas, ctx.MainWindow);
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions.Take(3).ToList(), 2);
            f.Section("Cota de nível");
            f.Combo("tipo", "Tipo de cota de nível", ProjectChoices.SpotTypes(doc), Choices.Or(ctx.Settings.Cotas.TipoCotaNivel, Choices.Default));
            f.Number("deslocamento", "Deslocamento abaixo do centro do ambiente", 10, "mm na folha",
                "Afasta a cota de nível da etiqueta do ambiente.");
            f.Check("pular", "Ignorar ambientes que já possuem cota de nível", true);
            if (!dlg.Run()) return Result.Cancelled;

            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"))
                .Where(r => r.LevelId == view.GenLevel.Id).ToList();
            if (rooms.Count == 0) throw new UserMessageException("Nenhum ambiente do nível desta planta foi selecionado.");

            SpotDimensionType spotType = Q.SpotElevationTypes(doc).FirstOrDefault(t => t.Name == Choices.Value(f.String("tipo")));
            double shift = Conv.PaperMm(f.Double("deslocamento"), view);
            var report = new Report(Title);

            using (var group = new TransactionGroup(doc, "DetalhaBIM - " + Title))
            {
                group.Start();
                var vt = new ViewTools(doc);
                bool created3D = false;
                View3D aux = null;
                Tx.Run(doc, "Vista auxiliar", () =>
                {
                    int before = Q.All<View3D>(doc).Count;
                    aux = vt.Aux3D();
                    created3D = Q.All<View3D>(doc).Count > before;
                });

                var existing = new List<XYZ>();
                if (f.Bool("pular"))
                {
                    existing = new FilteredElementCollector(doc, view.Id).OfClass(typeof(SpotDimension)).Cast<SpotDimension>()
                        .Select(sd => sd.Origin).Where(o => o != null).ToList();
                }

                var intersector = new ReferenceIntersector(new ElementCategoryFilter(BuiltInCategory.OST_Floors), FindReferenceTarget.Face, aux)
                {
                    FindReferencesInRevitLinks = false,
                };

                Tx.Run(doc, Title, () =>
                {
                    foreach (Room room in rooms)
                    {
                        if (existing.Any(o => RoomGeo.Contains(room, o)))
                        {
                            report.Count("ambientes já cotados (ignorados)");
                            continue;
                        }
                        XYZ p = RoomGeo.Point(room) - Geo.FlatDir(view.UpDirection) * shift;
                        if (!RoomGeo.Contains(room, p)) p = RoomGeo.Point(room);
                        XYZ origin = Geo.WithZ(p, RoomGeo.BaseElevation(room) + Conv.M(1.5));
                        ReferenceWithContext hit = intersector.FindNearest(origin, XYZ.BasisZ.Negate());
                        if (hit == null || hit.Proximity > Conv.M(3))
                        {
                            report.Warn($"Ambiente {RoomGeo.Label(room)}: nenhum piso encontrado sob o ambiente.");
                            continue;
                        }
                        Reference r = hit.GetReference();
                        XYZ q = r.GlobalPoint;
                        try
                        {
                            SpotDimension spot = doc.Create.NewSpotElevation(view, r, q, q, q, q, false);
                            if (spotType != null) spot.ChangeTypeId(spotType.Id);
                            report.Count("cotas de nível criadas");
                        }
                        catch
                        {
                            report.Warn($"Ambiente {RoomGeo.Label(room)}: o piso não aceitou cota de nível nesta vista.");
                        }
                    }
                }, deleteFailingAnnotations: true);

                if (created3D && aux != null)
                {
                    Tx.Run(doc, "Remover vista auxiliar", () => doc.Delete(aux.Id));
                }
                group.Assimilate();
            }

            report.Show();
            return Result.Succeeded;
        }
    }
}
