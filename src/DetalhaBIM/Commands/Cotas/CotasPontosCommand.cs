using System;
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
    /// <summary>
    /// Clique em dois pontos: todas as paredes, pilares e eixos atravessados pela linha são
    /// cotados (espessuras e vãos livres). Repete até pressionar ESC.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CotasPontosCommand : CommandBase
    {
        protected override string Title => "Cotas por Pontos";

        protected override Result Run(CommandContext ctx)
        {
            ViewPlan view = Guards.RequirePlan(ctx);
            Document doc = ctx.Doc;
            CotasSettings s = ctx.Settings.Cotas;

            var dlg = new OptionsDialog("cotas-pontos", Title,
                "Clique no ponto inicial e no ponto final: tudo o que a linha atravessar (paredes, pilares e eixos) é cotado em uma única cadeia. Continue clicando; ESC encerra.",
                Theme.Cotas, ctx.MainWindow, "Começar");
            FormBuilder f = dlg.Form;
            f.Combo("tipo", "Tipo de cota", ProjectChoices.DimensionTypes(doc), Choices.Or(s.TipoCota, Choices.Default));
            f.Check("orto", "Forçar direção ortogonal (alinhada às paredes do projeto)", true);
            f.Check("eixos", "Incluir eixos (grids) atravessados", true);
            f.Check("pilares", "Incluir pilares atravessados", true);
            f.Check("terceiro", "Posicionar a linha de cota com um 3º clique", false);
            if (!dlg.Run()) return Result.Cancelled;

            var builder = new DimensionBuilder(doc, view, Q.DimensionType(doc, Choices.Value(f.String("tipo"))), Conv.Cm(s.MenorSegmentoCm));
            var faces = new FaceFinder(view);
            List<Wall> walls = Q.WallsInView(doc, view);
            var elements = new List<Element>(walls);
            if (f.Bool("pilares"))
            {
                elements.AddRange(Q.InView(doc, view, BuiltInCategory.OST_Columns));
                elements.AddRange(Q.InView(doc, view, BuiltInCategory.OST_StructuralColumns));
            }
            List<Grid> grids = f.Bool("eixos") ? Q.GridsInView(doc, view) : new List<Grid>();
            PlanFrame frame = PlanFrame.FromWalls(walls);
            double z = view.GenLevel.Elevation;
            var report = new Report(Title);
            Guards.EnsureWorkPlane(doc, view);
            const ObjectSnapTypes snaps = ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Midpoints
                                          | ObjectSnapTypes.Nearest | ObjectSnapTypes.Perpendicular;

            while (true)
            {
                XYZ p1, p2, through;
                try
                {
                    p1 = Geo.WithZ(ctx.UiDoc.Selection.PickPoint(snaps, "Clique no ponto inicial da linha de cota (ESC para encerrar)"), z);
                    p2 = Geo.WithZ(ctx.UiDoc.Selection.PickPoint(snaps, "Clique no ponto final da linha de cota"), z);
                    if (f.Bool("orto")) p2 = Orthogonal(p1, p2, frame);
                    through = f.Bool("terceiro")
                        ? Geo.WithZ(ctx.UiDoc.Selection.PickPoint(ObjectSnapTypes.None, "Clique onde a linha de cota deve ficar"), z)
                        : p1;
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }
                if (p1.DistanceTo(p2) < Conv.Cm(5)) continue;

                XYZ d = Geo.FlatDir(p2 - p1);
                var refs = faces.Crossing(elements, p1, p2, Conv.Mm(1));
                refs.AddRange(GridsCrossing(grids, p1, p2, d));

                Tx.Run(doc, Title, () =>
                {
                    if (builder.Create(refs, d, through) != null) report.Count("cotas criadas");
                    else report.Warn("Uma das linhas não atravessou ao menos dois elementos cotáveis.");
                }, deleteFailingAnnotations: true);
            }

            if (report.Warnings.Count > 0) report.Show();
            return Result.Succeeded;
        }

        private static XYZ Orthogonal(XYZ p1, XYZ p2, PlanFrame frame)
        {
            XYZ v = p2 - p1;
            double x = v.DotProduct(frame.X), y = v.DotProduct(frame.Y);
            return Math.Abs(x) >= Math.Abs(y) ? p1 + frame.X * x : p1 + frame.Y * y;
        }

        private static IEnumerable<RefPos> GridsCrossing(IEnumerable<Grid> grids, XYZ a, XYZ b, XYZ d)
        {
            double len = Geo.Flat(b - a).GetLength();
            foreach (Grid g in grids)
            {
                if (!(g.Curve is Line gl) || !Geo.IsPerpendicular(gl.Direction, d)) continue;
                // Interseção 2D entre a linha clicada e o eixo.
                XYZ o = Geo.Flat(gl.GetEndPoint(0)), e = Geo.Flat(gl.GetEndPoint(1));
                double pos = o.DotProduct(d);
                double t = pos - Geo.Flat(a).DotProduct(d);
                if (t < 0 || t > len) continue;
                XYZ q = Geo.Flat(a) + d * t;
                XYZ gd = (e - o).Normalize();
                double s = (q - o).DotProduct(gd);
                if (s < -Conv.Cm(1) || s > o.DistanceTo(e) + Conv.Cm(1)) continue;
                yield return new RefPos(new Reference(g), pos, g, 1);
            }
        }
    }
}
