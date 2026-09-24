using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Organizacao
{
    /// <summary>
    /// Eixos automáticos a partir das paredes: um eixo por alinhamento, numerados e nomeados
    /// (1, 2, 3... / A, B, C...), com plantas de eixos cotadas para vários níveis de uma vez.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class EixosCommand : CommandBase
    {
        protected override string Title => "Eixos Automáticos";

        private class Axis
        {
            public bool Vertical;      // constante em X local (linha paralela ao eixo Y)
            public double Position;    // coordenada local
            public double Length;      // soma dos comprimentos das paredes
            public Grid Grid;
        }

        protected override Result Run(CommandContext ctx)
        {
            ViewPlan view = Guards.RequirePlan(ctx);
            Document doc = ctx.Doc;
            ElementId activeLevel = view.GenLevel.Id;

            var dlg = new OptionsDialog("eixos", Title,
                "Cria eixos (grids) nos alinhamentos das paredes, numera e nomeia automaticamente e gera plantas de eixos cotadas para os níveis escolhidos.",
                Theme.Organizacao, ctx.MainWindow, "Criar eixos", 580);
            FormBuilder f = dlg.Form;
            f.Section("Paredes de origem");
            f.Radio("origem", null, new[] { "Todas as paredes da vista ativa", "Somente as paredes selecionadas" }, 0);
            f.Number("comprimento", "Comprimento mínimo do alinhamento", 100, "cm");
            f.Number("espessura", "Espessura mínima da parede", 0, "cm", "Use para ignorar divisórias finas (ex.: 12 cm).");
            f.Number("tolerancia", "Tolerância de alinhamento", 10, "cm");
            f.Radio("referencia", "Eixo posicionado na", new[] { "Linha central da parede", "Face externa das paredes de fachada (demais no centro)" }, 0);
            f.Section("Nomenclatura");
            f.Radio("nomes", null, new[] { "Verticais 1, 2, 3...  |  Horizontais A, B, C...", "Verticais A, B, C...  |  Horizontais 1, 2, 3..." }, 0);
            f.Number("prolongamento", "Prolongamento além das paredes", 150, "cm");
            f.Check("reaproveitar", "Reaproveitar eixos existentes coincidentes", true);
            f.Section("Plantas de eixos");
            f.Checklist("niveis", "Criar \"PLANTA DE EIXOS\" para os níveis", Q.Levels(doc).Select(l => new CheckItem(l.Name, l, false)), 120);
            f.Combo("modelo", "Modelo de vista", ProjectChoices.Templates(doc, ViewType.FloorPlan), Choices.None);
            f.Check("cotarAtiva", "Cotar os eixos também na vista ativa", true);
            f.Combo("tipo", "Tipo de cota", ProjectChoices.DimensionTypes(doc), Choices.Or(ctx.Settings.Cotas.TipoCota, Choices.Default));
            if (!dlg.Run()) return Result.Cancelled;

            List<Wall> walls = f.Index("origem") == 1
                ? Pick.SelectedOrPick(ctx.UiDoc, new PredicateFilter(e => e is Wall), "Selecione as paredes e clique em Concluir").OfType<Wall>().ToList()
                : Q.WallsInView(doc, view);
            walls = walls.Where(w => w.Location is LocationCurve lc && lc.Curve is Line && w.Width >= Conv.Cm(f.Double("espessura")) - 1e-9).ToList();
            if (walls.Count == 0) throw new UserMessageException("Nenhuma parede reta atende aos critérios.");

            PlanFrame frame = PlanFrame.FromWalls(walls);
            double tol = Conv.Cm(Math.Max(1, f.Double("tolerancia")));
            double minLen = Conv.Cm(f.Double("comprimento"));
            double ext = Conv.Cm(f.Double("prolongamento"));
            bool facadeFaces = f.Index("referencia") == 1;

            // Coleta posições por direção.
            var samples = new List<(bool vertical, double pos, double len)>();
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            int skipped = 0;
            foreach (Wall w in walls)
            {
                Line line = Q.WallCenterline(w);
                XYZ d = Geo.FlatDir(line.Direction);
                foreach (XYZ p in new[] { line.GetEndPoint(0), line.GetEndPoint(1) })
                {
                    minX = Math.Min(minX, frame.LX(p));
                    maxX = Math.Max(maxX, frame.LX(p));
                    minY = Math.Min(minY, frame.LY(p));
                    maxY = Math.Max(maxY, frame.LY(p));
                }
                XYZ mid = Geo.Mid(line);
                if (Geo.IsParallel(d, frame.Y, 0.9998)) samples.Add((true, frame.LX(mid), line.Length));
                else if (Geo.IsParallel(d, frame.X, 0.9998)) samples.Add((false, frame.LY(mid), line.Length));
                else skipped++;
            }

            List<Axis> axes = Cluster(samples, tol, minLen);
            if (facadeFaces) SnapFacades(axes, walls, frame, minX, maxX, minY, maxY, tol);

            var report = new Report(Title);
            if (skipped > 0) report.Warn($"{skipped} parede(s) inclinada(s) em relação aos eixos principais foram ignoradas.");
            List<Level> levels = f.Checked<Level>("niveis");
            var vt = new ViewTools(doc);
            DimensionType dimType = Q.DimensionType(doc, Choices.Value(f.String("tipo")));
            bool letterVertical = f.Index("nomes") == 1;

            using (var group = new TransactionGroup(doc, "DetalhaBIM - " + Title))
            {
                group.Start();
                Tx.Run(doc, Title, () =>
                {
                    List<Grid> existing = Q.All<Grid>(doc);
                    var names = new HashSet<string>(existing.Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
                    List<Level> all = Q.Levels(doc);
                    double bottom = all.Min(l => l.Elevation) - Conv.M(1);
                    double top = all.Max(l => l.Elevation) + Conv.M(4);

                    List<Axis> verticals = axes.Where(a => a.Vertical).OrderBy(a => a.Position).ToList();
                    List<Axis> horizontals = axes.Where(a => !a.Vertical).OrderByDescending(a => a.Position).ToList();
                    for (int i = 0; i < verticals.Count; i++) Build(verticals[i], letterVertical ? Geo.Letters(i) : (i + 1).ToString());
                    for (int i = 0; i < horizontals.Count; i++) Build(horizontals[i], letterVertical ? (i + 1).ToString() : Geo.Letters(i));

                    void Build(Axis a, string name)
                    {
                        if (f.Bool("reaproveitar"))
                        {
                            a.Grid = existing.FirstOrDefault(g => g.Curve is Line gl && Coincident(gl, a, frame, tol));
                            if (a.Grid != null)
                            {
                                report.Count("eixos existentes reaproveitados");
                                return;
                            }
                        }
                        XYZ p0 = a.Vertical ? frame.World(a.Position, minY - ext, 0) : frame.World(maxX + ext, a.Position, 0);
                        XYZ p1 = a.Vertical ? frame.World(a.Position, maxY + ext, 0) : frame.World(minX - ext, a.Position, 0);
                        a.Grid = Grid.Create(doc, Line.CreateBound(p0, p1));
                        string unique = name;
                        while (names.Contains(unique)) unique += "'";
                        a.Grid.Name = unique;
                        names.Add(unique);
                        try
                        {
                            a.Grid.SetVerticalExtents(bottom, top);
                        }
                        catch
                        {
                            // mantém a extensão padrão
                        }
                        report.Count("eixos criados");
                    }

                    doc.Regenerate();
                    var views = new List<ViewPlan>();
                    if (f.Bool("cotarAtiva")) views.Add(view);
                    ViewFamilyType floor = vt.FamilyType(ViewFamily.FloorPlan);
                    foreach (Level level in levels)
                    {
                        ViewPlan v = ViewPlan.Create(doc, floor.Id, level.Id);
                        vt.SetName(v, "PLANTA DE EIXOS - " + level.Name);
                        vt.ApplyTemplate(v, Choices.Value(f.String("modelo")), report);
                        views.Add(v);
                        report.Count("plantas de eixos criadas");
                    }
                    doc.Regenerate();

                    foreach (ViewPlan v in views)
                    {
                        int n = Dimension(doc, v, dimType, frame, axes, minX, maxX, minY, maxY, ext, ctx.Settings.Cotas);
                        report.Count("cotas de eixos", n);
                    }
                }, deleteFailingAnnotations: true);
                group.Assimilate();
            }

            report.Show();
            return Result.Succeeded;
        }

        /// <summary>Agrupa posições próximas (média ponderada pelo comprimento das paredes).</summary>
        private static List<Axis> Cluster(List<(bool vertical, double pos, double len)> samples, double tol, double minLen)
        {
            var result = new List<Axis>();
            foreach (var dir in samples.GroupBy(s => s.vertical))
            {
                var sorted = dir.OrderBy(s => s.pos).ToList();
                var bucket = new List<(bool vertical, double pos, double len)>();
                void Flush()
                {
                    if (bucket.Count == 0) return;
                    double len = bucket.Sum(b => b.len);
                    if (len >= minLen)
                    {
                        result.Add(new Axis { Vertical = dir.Key, Position = bucket.Sum(b => b.pos * b.len) / len, Length = len });
                    }
                    bucket.Clear();
                }
                foreach (var s in sorted)
                {
                    if (bucket.Count > 0 && s.pos - bucket[0].pos > tol) Flush();
                    bucket.Add(s);
                }
                Flush();
            }
            return result;
        }

        /// <summary>Move os eixos extremos para a face externa das paredes de fachada.</summary>
        private static void SnapFacades(List<Axis> axes, List<Wall> walls, PlanFrame frame, double minX, double maxX, double minY, double maxY, double tol)
        {
            foreach (Axis a in axes)
            {
                bool first = a.Vertical ? Math.Abs(a.Position - minX) < tol * 2 : Math.Abs(a.Position - minY) < tol * 2;
                bool last = a.Vertical ? Math.Abs(a.Position - maxX) < tol * 2 : Math.Abs(a.Position - maxY) < tol * 2;
                if (!first && !last) continue;
                double width = walls.Where(w =>
                {
                    XYZ mid = Geo.Mid(Q.WallCenterline(w));
                    return Math.Abs((a.Vertical ? frame.LX(mid) : frame.LY(mid)) - a.Position) < tol;
                }).Select(w => w.Width).DefaultIfEmpty(0).Max();
                a.Position += (first ? -1 : 1) * width / 2;
            }
        }

        private static bool Coincident(Line gl, Axis a, PlanFrame frame, double tol)
        {
            XYZ d = Geo.FlatDir(gl.Direction);
            if (a.Vertical && Geo.IsParallel(d, frame.Y, 0.9998)) return Math.Abs(frame.LX(gl.Origin) - a.Position) < tol;
            if (!a.Vertical && Geo.IsParallel(d, frame.X, 0.9998)) return Math.Abs(frame.LY(gl.Origin) - a.Position) < tol;
            return false;
        }

        /// <summary>Cadeia parcial + total acima (eixos verticais) e à esquerda (eixos horizontais).</summary>
        private static int Dimension(Document doc, ViewPlan v, DimensionType type, PlanFrame frame, List<Axis> axes,
            double minX, double maxX, double minY, double maxY, double ext, CotasSettings cs)
        {
            var builder = new DimensionBuilder(doc, v, type, Conv.Mm(5));
            double z = v.GenLevel.Elevation;
            double first = Math.Min(ext * 0.45, Conv.PaperMm(cs.PrimeiraLinhaMm + 6, v));
            double step = Conv.PaperMm(cs.EspacamentoMm, v);
            int created = 0;

            List<RefPos> Refs(bool vertical, XYZ dir) => axes.Where(a => a.Vertical == vertical && a.Grid != null)
                .Select(a => new RefPos(new Reference(a.Grid), ((Line)a.Grid.Curve).Origin.DotProduct(dir), a.Grid)).ToList();

            List<RefPos> vx = Refs(true, frame.X);
            if (vx.Count >= 2)
            {
                if (builder.Create(vx, frame.X, frame.World(0, maxY + first, z)) != null) created++;
                if (vx.Count > 2 && builder.Create(builder.Extremes(vx), frame.X, frame.World(0, maxY + first + step, z)) != null) created++;
            }
            List<RefPos> hy = Refs(false, frame.Y);
            if (hy.Count >= 2)
            {
                if (builder.Create(hy, frame.Y, frame.World(minX - first, 0, z)) != null) created++;
                if (hy.Count > 2 && builder.Create(builder.Extremes(hy), frame.Y, frame.World(minX - first - step, 0, z)) != null) created++;
            }
            return created;
        }
    }
}
