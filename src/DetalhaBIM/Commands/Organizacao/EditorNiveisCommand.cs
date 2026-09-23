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
    /// <summary>Editor de Níveis: todas as ações sobre níveis em uma única tabela.</summary>
    [Transaction(TransactionMode.Manual)]
    public class EditorNiveisCommand : CommandBase
    {
        protected override string Title => "Editor de Níveis";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            List<ViewPlan> plans = Q.All<ViewPlan>(doc).Where(v => !v.IsTemplate && v.GenLevel != null).ToList();
            List<LevelRow> rows = Q.Levels(doc).OrderByDescending(l => l.Elevation).Select(l => new LevelRow
            {
                LevelId = l.Id.Value,
                OriginalName = l.Name,
                Name = l.Name,
                OriginalElevationM = Math.Round(Conv.ToM(l.Elevation), 4),
                Elevation = Conv.Format(Conv.ToM(l.Elevation), 3),
                PlanCount = plans.Count(p => p.GenLevel.Id == l.Id),
            }).ToList();
            // A comparação de "mover" usa o valor exibido (3 casas), evitando falsos positivos.
            foreach (LevelRow r in rows) r.OriginalElevationM = Conv.TryParse(r.Elevation, out double m) ? m : r.OriginalElevationM;

            var window = new LevelEditorWindow(rows, ctx.MainWindow);
            if (window.ShowDialog() != true) return Result.Cancelled;

            List<LevelRow> all = window.Rows.ToList();
            List<LevelRow> toDelete = all.Where(r => r.Delete && !r.IsNew).ToList();
            if (toDelete.Count > 0)
            {
                int views = toDelete.Sum(r => r.PlanCount);
                if (!Dialogs.Confirm(Title, $"Excluir {toDelete.Count} nível(is)?",
                        $"As {views} planta(s) associadas e todos os elementos hospedados nesses níveis também serão excluídos pelo Revit.\n\nNíveis: {string.Join(", ", toDelete.Select(r => r.OriginalName))}"))
                {
                    toDelete.Clear();
                }
            }

            var report = new Report(Title);
            bool renameViews = window.RenameViews.IsChecked == true;
            var vt = new ViewTools(doc);

            using (var group = new TransactionGroup(doc, "DetalhaBIM - " + Title))
            {
                group.Start();
                Tx.Run(doc, Title, () =>
                {
                    // 1) Exclusões
                    foreach (LevelRow r in toDelete)
                    {
                        try
                        {
                            doc.Delete(new ElementId(r.LevelId.Value));
                            report.Count("níveis excluídos");
                        }
                        catch (Exception ex)
                        {
                            report.Warn($"Não foi possível excluir {r.OriginalName}: {ex.Message}");
                        }
                    }

                    List<LevelRow> keep = all.Where(r => !r.Delete).ToList();

                    // 2) Renomeação em duas etapas (permite trocar nomes entre níveis).
                    List<LevelRow> renamed = keep.Where(r => r.Renamed).ToList();
                    foreach (LevelRow r in renamed) Level(doc, r).Name = "DetalhaBIM-temp-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    foreach (LevelRow r in renamed)
                    {
                        Level level = Level(doc, r);
                        level.Name = r.Name.Trim();
                        report.Count("níveis renomeados");
                        if (renameViews) RenameViews(doc, level, r.OriginalName, r.Name.Trim(), report);
                    }

                    // 3) Elevações
                    foreach (LevelRow r in keep.Where(r => r.Moved))
                    {
                        Conv.TryParse(r.Elevation, out double m);
                        Level(doc, r).Elevation = Conv.M(m);
                        report.Count("níveis com elevação alterada");
                    }

                    // 4) Novos níveis
                    foreach (LevelRow r in keep.Where(r => r.IsNew))
                    {
                        Conv.TryParse(r.Elevation, out double m);
                        Level level = Autodesk.Revit.DB.Level.Create(doc, Conv.M(m));
                        try
                        {
                            level.Name = r.Name.Trim();
                        }
                        catch
                        {
                            report.Warn($"Nome \"{r.Name}\" recusado pelo Revit; mantido \"{level.Name}\".");
                        }
                        r.LevelId = level.Id.Value;
                        report.Count("níveis criados");
                    }

                    // 5) Plantas
                    ViewFamilyType floor = vt.FamilyType(ViewFamily.FloorPlan);
                    ViewFamilyType ceiling = vt.FamilyType(ViewFamily.CeilingPlan);
                    foreach (LevelRow r in keep.Where(r => r.LevelId.HasValue))
                    {
                        Level level = Level(doc, r);
                        if (r.CreateFloorPlan && floor != null)
                        {
                            ViewPlan v = ViewPlan.Create(doc, floor.Id, level.Id);
                            vt.SetName(v, level.Name);
                            report.Count("plantas de piso criadas");
                        }
                        if (r.CreateCeilingPlan && ceiling != null)
                        {
                            ViewPlan v = ViewPlan.Create(doc, ceiling.Id, level.Id);
                            vt.SetName(v, level.Name + " - FORRO");
                            report.Count("plantas de forro criadas");
                        }
                    }
                });
                group.Assimilate();
            }

            report.Show("Nenhuma alteração foi feita.");
            return Result.Succeeded;
        }

        private static Level Level(Document doc, LevelRow r) => (Level)doc.GetElement(new ElementId(r.LevelId.Value));

        private static void RenameViews(Document doc, Level level, string oldName, string newName, Report report)
        {
            if (string.IsNullOrEmpty(oldName)) return;
            foreach (ViewPlan v in Q.All<ViewPlan>(doc).Where(v => !v.IsTemplate && v.GenLevel?.Id == level.Id))
            {
                if (v.Name.IndexOf(oldName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string name = v.Name.Replace(oldName, newName);
                try
                {
                    v.Name = name;
                    report.Count("plantas renomeadas");
                }
                catch
                {
                    report.Warn($"A planta \"{v.Name}\" não pôde ser renomeada para \"{name}\" (nome já existente).");
                }
            }
        }
    }
}
