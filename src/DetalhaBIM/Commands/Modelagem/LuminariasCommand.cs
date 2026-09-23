using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Modelagem
{
    /// <summary>
    /// Distribui luminárias em malha uniforme em cada ambiente (meio espaçamento junto às
    /// paredes), hospedando no forro quando existir.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LuminariasCommand : CommandBase
    {
        protected override string Title => "Distribuir Luminárias";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            ModelagemSettings ms = ctx.Settings.Modelagem;
            List<string> types = Choices.Symbols(doc, BuiltInCategory.OST_LightingFixtures, false);
            if (types.Count == 0) throw new UserMessageException("Carregue ao menos uma família de luminária no projeto.");

            var dlg = new OptionsDialog("luminarias", Title,
                "Distribui luminárias em malha uniforme dentro de cada ambiente, com meio espaçamento junto às paredes, seguindo o forro.",
                Theme.Modelagem, ctx.MainWindow, "Distribuir");
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions, 0);
            f.Section("Luminária");
            f.Combo("tipo", "Família : Tipo", types, Choices.Or(ms.TipoLuminaria, types[0]));
            f.Number("espacamento", "Espaçamento máximo entre luminárias", ms.EspacamentoLuminariaCm, "cm");
            f.Number("afastamento", "Afastamento mínimo das paredes", ms.AfastamentoLuminariaCm, "cm");
            f.Number("altura", "Altura de instalação (sem forro)", ms.AlturaForroCm, "cm");
            f.Check("alinhar", "Alinhar a malha à direção das paredes do ambiente", true);
            f.Check("forro", "Hospedar no forro quando existir", true);
            if (!dlg.Run()) return Result.Cancelled;

            FamilySymbol symbol = Q.SymbolByLabel(doc, BuiltInCategory.OST_LightingFixtures, f.String("tipo"));
            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"));
            double spacing = Conv.Cm(Math.Max(20, f.Double("espacamento")));
            double margin = Conv.Cm(Math.Max(0, f.Double("afastamento")));
            double height = Conv.Cm(f.Double("altura"));
            bool align = f.Bool("alinhar");
            bool hostCeiling = f.Bool("forro");
            var report = new Report(Title);
            FamilyPlacementType placement = symbol.Family.FamilyPlacementType;

            using (var group = new TransactionGroup(doc, "DetalhaBIM - " + Title))
            {
                group.Start();
                var vt = new ViewTools(doc);
                View3D aux = null;
                bool created3D = false;
                Tx.Run(doc, "Vista auxiliar", () =>
                {
                    int before = Q.All<View3D>(doc).Count;
                    aux = vt.Aux3D();
                    created3D = Q.All<View3D>(doc).Count > before;
                    if (!symbol.IsActive) symbol.Activate();
                });
                var intersector = new ReferenceIntersector(new ElementCategoryFilter(BuiltInCategory.OST_Ceilings), FindReferenceTarget.Face, aux)
                {
                    FindReferencesInRevitLinks = false,
                };

                Tx.Run(doc, Title, () =>
                {
                    foreach (Room room in rooms)
                    {
                        int n = 0;
                        foreach (XYZ p in GridPoints(room, spacing, margin, align, out PlanFrame frame))
                        {
                            if (Place(doc, room, symbol, placement, p, frame, height, hostCeiling ? intersector : null)) n++;
                        }
                        if (n == 0) report.Warn($"Ambiente {RoomGeo.Label(room)}: nenhuma luminária pôde ser inserida.");
                        report.Count("luminárias inseridas", n);
                    }
                });

                if (created3D && aux != null) Tx.Run(doc, "Remover vista auxiliar", () => doc.Delete(aux.Id));
                group.Assimilate();
            }

            report.Count("ambientes processados", rooms.Count);
            report.Show();
            return Result.Succeeded;
        }

        /// <summary>Malha uniforme: n = arredondar(L / espaçamento), meio espaçamento junto às paredes.</summary>
        private static List<XYZ> GridPoints(Room room, double spacing, double margin, bool align, out PlanFrame frame)
        {
            XYZ origin = RoomGeo.Point(room);
            frame = align ? RoomGeo.Frame(room) : new PlanFrame(origin, XYZ.BasisX);
            RoomGeo.Bounds(RoomGeo.BoundaryPoints(room), frame, out double minX, out double maxX, out double minY, out double maxY);

            int Count(double length)
            {
                int n = Math.Max(1, (int)Math.Round(length / spacing));
                while (n > 1 && length / n / 2 < margin) n--;
                return n;
            }

            double w = maxX - minX, h = maxY - minY;
            int nx = Count(w), ny = Count(h);
            var pts = new List<XYZ>();
            double z = RoomGeo.BaseElevation(room);
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                XYZ p = frame.World(minX + (i + 0.5) * w / nx, minY + (j + 0.5) * h / ny, z);
                if (RoomGeo.Contains(room, p)) pts.Add(p);
            }
            return pts;
        }

        private static bool Place(Document doc, Room room, FamilySymbol symbol, FamilyPlacementType placement, XYZ p, PlanFrame frame,
            double defaultHeight, ReferenceIntersector intersector)
        {
            Level level = room.Level;
            ReferenceWithContext hit = intersector?.FindNearest(Geo.WithZ(p, RoomGeo.BaseElevation(room) + Conv.Cm(50)), XYZ.BasisZ);
            if (hit != null && hit.Proximity > RoomGeo.TopElevation(room) - RoomGeo.BaseElevation(room) + Conv.Cm(50)) hit = null;

            return Tx.TrySub(doc, () =>
            {
                FamilyInstance fi = null;
                if (hit != null && placement == FamilyPlacementType.WorkPlaneBased)
                {
                    Reference face = hit.GetReference();
                    fi = doc.Create.NewFamilyInstance(face, face.GlobalPoint, frame.X, symbol);
                }
                else if (hit != null && placement == FamilyPlacementType.OneLevelBasedHosted)
                {
                    Element ceiling = doc.GetElement(hit.GetReference());
                    fi = doc.Create.NewFamilyInstance(hit.GetReference().GlobalPoint, symbol, ceiling, level, StructuralType.NonStructural);
                }
                else if (placement == FamilyPlacementType.OneLevelBasedHosted)
                {
                    throw new InvalidOperationException("Família hospedada em forro sem forro no ambiente.");
                }
                else
                {
                    double z = hit != null ? hit.GetReference().GlobalPoint.Z - level.Elevation : defaultHeight;
                    fi = doc.Create.NewFamilyInstance(Geo.WithZ(p, level.Elevation), symbol, level, StructuralType.NonStructural);
                    if (!Q.Set(fi, BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM, z)) Q.Set(fi, BuiltInParameter.INSTANCE_ELEVATION_PARAM, z);
                    if (Math.Abs(frame.Angle) > 1e-6)
                    {
                        ElementTransformUtils.RotateElement(doc, fi.Id, Line.CreateBound(Geo.WithZ(p, 0), Geo.WithZ(p, 1)), frame.Angle);
                    }
                }
                if (fi == null) throw new InvalidOperationException();
            });
        }
    }
}
