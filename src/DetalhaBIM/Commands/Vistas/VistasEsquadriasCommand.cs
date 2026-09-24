using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Vistas
{
    /// <summary>
    /// Uma elevação cotada para cada código de esquadria (P01, J01...): largura, altura e peitoril,
    /// pronta para compor o quadro/caderno de esquadrias.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class VistasEsquadriasCommand : CommandBase
    {
        protected override string Title => "Vistas de Esquadrias";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            DetalhaSettings s = ctx.Settings;

            var dlg = new OptionsDialog("vistas-esquadrias", Title,
                "Cria uma elevação para cada tipo de porta e janela (P01, J01...), recortada e cotada com largura, altura e peitoril.",
                Theme.Vistas, ctx.MainWindow, "Criar vistas");
            FormBuilder f = dlg.Form;
            f.Section("Esquadrias");
            f.Check("portas", "Portas", true);
            f.Check("janelas", "Janelas", true);
            f.Radio("agrupar", "Uma vista para cada", new[] { "Marca de tipo (P01, J01...)", "Tipo de família" }, 0);
            f.Radio("lado", "Vista pelo lado", new[] { "Externo (face da família)", "Interno" }, 0);
            f.Section("Vista");
            f.Combo("escala", "Escala", Choices.Scales, Choices.Scale(s.Vistas.EscalaEsquadria), true);
            f.Combo("modelo", "Modelo de vista", ProjectChoices.Templates(doc, ViewType.Elevation, ViewType.Section), Choices.Or(s.Vistas.ModeloEsquadria, Choices.None));
            f.Check("cotar", "Cotar largura, altura e peitoril", true);
            f.Combo("tipo", "Tipo de cota", ProjectChoices.DimensionTypes(doc), Choices.Or(s.Cotas.TipoCota, Choices.Default));
            f.Check("ocultar", "Ocultar os marcadores de elevação nas plantas", true);
            f.Section("Prancha");
            f.Check("prancha", "Montar prancha(s) com todas as vistas de esquadrias", true);
            f.Combo("carimbo", "Carimbo (folha)", ProjectChoices.Symbols(doc, BuiltInCategory.OST_TitleBlocks, false), s.Pranchas.Carimbo);
            if (!dlg.Run()) return Result.Cancelled;

            var cats = new List<BuiltInCategory>();
            if (f.Bool("portas")) cats.Add(BuiltInCategory.OST_Doors);
            if (f.Bool("janelas")) cats.Add(BuiltInCategory.OST_Windows);
            if (cats.Count == 0) return Result.Cancelled;

            bool byMark = f.Index("agrupar") == 0;
            List<(string key, FamilyInstance fi)> groups = Representatives(doc, cats, byMark);
            if (groups.Count == 0) throw new UserMessageException("Nenhuma porta ou janela hospedada em parede foi encontrada.");

            int scale = Choices.ParseScale(f.String("escala"), s.Vistas.EscalaEsquadria);
            var report = new Report(Title);
            var vt = new ViewTools(doc);
            ElementId elevType = vt.FamilyType(ViewFamily.Elevation)?.Id
                                 ?? throw new UserMessageException("O projeto não possui tipo de vista de elevação.");
            DimensionType dimType = Q.DimensionType(doc, Choices.Value(f.String("tipo")));
            var created = new List<View>();

            using (var group = new TransactionGroup(doc, "DetalhaBIM - " + Title))
            {
                group.Start();
                Tx.Run(doc, Title, () =>
                {
                    foreach ((string key, FamilyInstance fi) in groups)
                    {
                        try
                        {
                            ViewSection v = CreateView(doc, vt, elevType, fi, key, scale, f.Index("lado") == 1, ctx.ActiveView, report);
                            if (v == null) continue;
                            vt.ApplyTemplate(v, Choices.Value(f.String("modelo")), report);
                            if (f.Bool("ocultar")) ElevationFactory.HideMarkerInPlans(v);
                            if (f.Bool("cotar")) Dimension(doc, v, fi, new DimensionBuilder(doc, v, dimType, Conv.Mm(5)), s.Cotas, report);
                            created.Add(v);
                            report.Count("vistas de esquadrias");
                        }
                        catch (System.Exception ex)
                        {
                            report.Warn($"{key}: {ex.Message}");
                        }
                    }
                }, deleteFailingAnnotations: true);

                if (f.Bool("prancha") && created.Count > 0)
                {
                    Tx.Run(doc, "Prancha de esquadrias", () =>
                    {
                        var sheets = new SheetService(doc, report, s.Pranchas);
                        report.Count("pranchas", sheets.Layout(created, sheets.TitleBlock(f.String("carimbo")), "DETALHAMENTO DE ESQUADRIAS").Count);
                    });
                }
                group.Assimilate();
            }

            report.Show();
            return Result.Succeeded;
        }

        /// <summary>Um representante por marca de tipo (ou tipo), em ordem natural: portas, depois janelas.</summary>
        private static List<(string key, FamilyInstance fi)> Representatives(Document doc, List<BuiltInCategory> cats, bool byMark)
        {
            var result = new List<(string, FamilyInstance)>();
            foreach (BuiltInCategory cat in cats)
            {
                var instances = new FilteredElementCollector(doc).OfCategory(cat).OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>().Where(fi => fi.Host is Wall && fi.Location is LocationPoint).ToList();
                var byKey = instances.GroupBy(fi =>
                {
                    string mark = Q.Text(fi.Symbol, BuiltInParameter.ALL_MODEL_TYPE_MARK);
                    return byMark && !string.IsNullOrWhiteSpace(mark) ? mark : Q.Label(fi.Symbol);
                });
                result.AddRange(byKey.OrderBy(g => g.Key, NaturalComparer.Instance)
                    .Select(g => (g.Key, g.OrderBy(fi => Q.Level(doc, fi)?.Elevation ?? 0).First())));
            }
            return result;
        }

        private static ViewSection CreateView(Document doc, ViewTools vt, ElementId elevType, FamilyInstance fi, string key, int scale,
            bool interior, View active, Report report)
        {
            Level level = Q.Level(doc, fi);
            if (level == null) return null;
            ViewPlan host = vt.FloorPlanOf(level.Id, active);
            if (host == null)
            {
                host = ViewPlan.Create(doc, vt.FamilyType(ViewFamily.FloorPlan).Id, level.Id);
                vt.SetName(host, level.Name);
            }

            var wall = (Wall)fi.Host;
            XYZ facing = Geo.FlatDir(fi.FacingOrientation);
            if (facing.IsZeroLength()) facing = Geo.LeftNormal(((LocationCurve)wall.Location).Curve.GetEndPoint(1) - ((LocationCurve)wall.Location).Curve.GetEndPoint(0));
            if (interior) facing = facing.Negate();

            XYZ loc = ((LocationPoint)fi.Location).Point;
            double dist = wall.Width / 2 + Conv.M(1);
            ViewSection v = ElevationFactory.Create(doc, elevType, host, loc + facing * dist, facing, scale);
            vt.SetName(v, "ESQUADRIA " + key);
            ViewTools.SetScale(v, scale);

            Geometry(fi, out double width, out double sill, out double height);
            double baseZ = level.Elevation;
            XYZ right = v.RightDirection;
            var pts = new[]
            {
                Geo.WithZ(loc - right * (width / 2), baseZ), Geo.WithZ(loc + right * (width / 2), baseZ),
                Geo.WithZ(loc - right * (width / 2), baseZ + sill + height), Geo.WithZ(loc + right * (width / 2), baseZ + sill + height),
            };
            ViewTools.CropToPoints(v, pts, Conv.PaperMm(28, v), Conv.PaperMm(10, v));
            ElevationFactory.SetFarClip(v, dist + wall.Width / 2 + Conv.Cm(20));
            return v;
        }

        private static void Geometry(FamilyInstance fi, out double width, out double sill, out double height)
        {
            width = Q.OpeningWidth(fi) ?? 0;
            height = Q.OpeningHeight(fi) ?? 0;
            sill = Q.Double(fi, BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM) ?? 0;
            BoundingBoxXYZ bb = fi.get_BoundingBox(null);
            if (bb != null)
            {
                if (width <= 0 && Geo.ExtentAlong(bb, Geo.FlatDir(fi.HandOrientation), out double min, out double max)) width = max - min;
                if (height <= 0) height = bb.Max.Z - bb.Min.Z;
            }
            if (width <= 0) width = Conv.M(1);
            if (height <= 0) height = Conv.M(2.1);
        }

        /// <summary>Largura (acima), altura (à direita) e peitoril (à esquerda) na elevação.</summary>
        private static void Dimension(Document doc, ViewSection v, FamilyInstance fi, DimensionBuilder builder, CotasSettings cs, Report report)
        {
            Geometry(fi, out double width, out double sill, out double height);
            Level level = Q.Level(doc, fi);
            double baseZ = level.Elevation;
            XYZ loc = ((LocationPoint)fi.Location).Point;
            XYZ right = v.RightDirection;
            double c = loc.DotProduct(right);
            double off = Conv.PaperMm(cs.PrimeiraLinhaMm, v);

            Reference Ref(FamilyInstanceReferenceType t) => fi.GetReferences(t).FirstOrDefault();
            Reference left = Ref(FamilyInstanceReferenceType.Left), rightRef = Ref(FamilyInstanceReferenceType.Right);
            Reference bottom = Ref(FamilyInstanceReferenceType.Bottom), top = Ref(FamilyInstanceReferenceType.Top);
            Reference levelRef = level.GetPlaneReference();
            if (bottom == null && sill < Conv.Cm(1)) bottom = levelRef;

            bool ok = false;
            if (left != null && rightRef != null)
            {
                XYZ through = Geo.WithZ(loc, baseZ + sill + height + off);
                ok |= builder.Create(new[] { new RefPos(left, c - width / 2, fi), new RefPos(rightRef, c + width / 2, fi) }, right, through) != null;
            }
            if (bottom != null && top != null)
            {
                XYZ through = loc + right * (width / 2 + off);
                ok |= builder.Create(new[] { new RefPos(bottom, baseZ + sill, fi), new RefPos(top, baseZ + sill + height, fi) }, XYZ.BasisZ, through) != null;
            }
            if (sill >= Conv.Cm(1) && bottom != null && levelRef != null)
            {
                XYZ through = loc - right * (width / 2 + off);
                builder.Create(new[] { new RefPos(levelRef, baseZ, level, 1), new RefPos(bottom, baseZ + sill, fi) }, XYZ.BasisZ, through);
            }
            if (!ok)
            {
                report.Warn($"A família \"{fi.Symbol.FamilyName}\" não possui planos de referência Esquerda/Direita/Superior definidos como referências — ajuste a família para cotas automáticas.");
            }
        }
    }
}
