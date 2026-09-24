using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DetalhaBIM.Core;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Cotas
{
    /// <summary>Uma cadeia de cotas com todos os elementos selecionados (paredes, eixos, pilares, esquadrias, planos).</summary>
    [Transaction(TransactionMode.Manual)]
    public class CotasSelecaoCommand : CommandBase
    {
        protected override string Title => "Cotas por Seleção";

        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_Walls, BuiltInCategory.OST_Grids, BuiltInCategory.OST_Columns,
            BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_CLines,
        };

        protected override Result Run(CommandContext ctx)
        {
            ViewPlan view = Guards.RequirePlan(ctx);
            Document doc = ctx.Doc;
            CotasSettings s = ctx.Settings.Cotas;

            var dlg = new OptionsDialog("cotas-selecao", Title,
                "Selecione paredes, eixos, pilares, portas, janelas ou planos de referência e clique onde a linha de cota deve passar. Uma única cadeia é criada com todos eles.",
                Theme.Cotas, ctx.MainWindow, "Selecionar");
            FormBuilder f = dlg.Form;
            f.Combo("tipo", "Tipo de cota", ProjectChoices.DimensionTypes(doc), Choices.Or(s.TipoCota, Choices.Default));
            f.Radio("direcao", "Direção da cota", new[] { "Automática (perpendicular ao 1º elemento)", "Horizontal na vista", "Vertical na vista" }, 0);
            f.Radio("esquadrias", "Portas e janelas", new[] { "Cotar o eixo", "Cotar o vão (esquerda e direita)" }, 1);
            if (!dlg.Run()) return Result.Cancelled;

            List<Element> elements = Pick.SelectedOrPick(ctx.UiDoc, PredicateFilter.Categories(Categories),
                "Selecione os elementos a cotar e clique em Concluir");
            if (elements.Count == 0) return Result.Cancelled;

            XYZ d = Direction(elements, view, f.Index("direcao"));
            Guards.EnsureWorkPlane(doc, view);
            XYZ point = ctx.UiDoc.Selection.PickPoint(ObjectSnapTypes.None, "Clique onde a linha de cota deve passar");
            point = Geo.WithZ(point, view.GenLevel.Elevation);

            var faces = new FaceFinder(view);
            var refs = new List<RefPos>();
            bool openingEdges = f.Index("esquadrias") == 1;
            foreach (Element e in elements) refs.AddRange(References(e, d, faces, openingEdges));

            var report = new Report(Title);
            var builder = new DimensionBuilder(doc, view, Q.DimensionType(doc, Choices.Value(f.String("tipo"))), Conv.Cm(s.MenorSegmentoCm));
            Tx.Run(doc, Title, () =>
            {
                if (builder.Create(refs, d, point) != null) report.Count("cota criada");
                else report.Warn("Os elementos selecionados não possuem faces paralelas entre si nesta direção. Tente escolher a direção manualmente.");
            }, deleteFailingAnnotations: true);

            if (report.Warnings.Count > 0) report.Show();
            return Result.Succeeded;
        }

        private static XYZ Direction(List<Element> elements, View view, int mode)
        {
            if (mode == 1) return Geo.FlatDir(view.RightDirection);
            if (mode == 2) return Geo.FlatDir(view.UpDirection);
            foreach (Element e in elements)
            {
                if (e is Wall w && w.Location is LocationCurve lc && lc.Curve is Line wl) return Geo.LeftNormal(wl.Direction);
                if (e is Grid g && g.Curve is Line gl) return Geo.LeftNormal(gl.Direction);
                if (e is ReferencePlane rp) return Geo.FlatDir(rp.Normal);
                if (e is FamilyInstance fi && Q.IsOpening(fi)) return Geo.FlatDir(fi.HandOrientation);
            }
            return Geo.FlatDir(view.RightDirection);
        }

        private static IEnumerable<RefPos> References(Element e, XYZ d, FaceFinder faces, bool openingEdges)
        {
            switch (e)
            {
                case Grid g when g.Curve is Line gl && Geo.IsPerpendicular(gl.Direction, d):
                    yield return new RefPos(new Reference(g), gl.Origin.DotProduct(d), g, 1);
                    yield break;
                case ReferencePlane rp when Geo.IsParallel(rp.Normal, d):
                    yield return new RefPos(rp.GetReference(), rp.BubbleEnd.DotProduct(d), rp, 1);
                    yield break;
                case FamilyInstance fi when Q.IsOpening(fi):
                    if (!Geo.IsParallel(fi.HandOrientation, d)) yield break;
                    double c = Geo.ElementPoint(fi).DotProduct(d);
                    if (openingEdges)
                    {
                        double half = (Q.OpeningWidth(fi) ?? 0) / 2;
                        Reference l = fi.GetReferences(FamilyInstanceReferenceType.Left).FirstOrDefault();
                        Reference r = fi.GetReferences(FamilyInstanceReferenceType.Right).FirstOrDefault();
                        if (l != null) yield return new RefPos(l, c - half, fi, 2);
                        if (r != null) yield return new RefPos(r, c + half, fi, 2);
                    }
                    else
                    {
                        Reference center = fi.GetReferences(FamilyInstanceReferenceType.CenterLeftRight).FirstOrDefault();
                        if (center != null) yield return new RefPos(center, c, fi, 2);
                    }
                    yield break;
            }

            foreach (FaceInfo face in faces.FacesNormalTo(e, d))
            {
                yield return new RefPos(face.Reference, face.PositionAlong(d), e, 3);
            }
        }
    }
}
