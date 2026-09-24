using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Vistas
{
    /// <summary>
    /// Cria o conjunto de plantas técnicas (layout, cotas, forro, pisos, pontos...) para os níveis
    /// escolhidos, com nomes, escalas e modelos padronizados — e já etiquetadas/cotadas.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PlantasTecnicasCommand : CommandBase
    {
        protected override string Title => "Plantas Técnicas";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            DetalhaSettings s = ctx.Settings;
            ElementId activeLevel = ctx.ActiveView?.GenLevel?.Id;

            var dlg = new OptionsDialog("plantas-tecnicas", Title,
                "Escolha os níveis e as plantas: o DetalhaBIM cria todas de uma vez, com nome, escala e modelo de vista padronizados, e já aplica etiquetas e cotas conforme cada planta.",
                Theme.Vistas, ctx.MainWindow, "Criar plantas", 600);
            FormBuilder f = dlg.Form;
            f.Checklist("niveis", "Níveis", Q.Levels(doc).Select(l =>
                new CheckItem(l.Name, l, l.Id == activeLevel, $"{Conv.Format(Conv.ToM(l.Elevation))} m")), 150);
            f.Checklist("plantas", "Plantas técnicas", s.PlantasTecnicas.Select(p =>
                new CheckItem(p.Nome, p, p.Ativa, Describe(p))), 190);
            f.Hint("Personalize nomes, modelos de vista, escalas e automações de cada planta em DetalhaBIM › Configurações.");
            f.Section("Opções");
            f.Check("pular", "Não recriar plantas que já existem (mesmo nome)", true);
            f.Combo("tipo", "Tipo de cota (cotas automáticas)", ProjectChoices.DimensionTypes(doc), Choices.Or(s.Cotas.TipoCota, Choices.Default));
            f.Check("prancha", "Criar uma prancha para cada planta", false);
            f.Combo("carimbo", "Carimbo (folha)", ProjectChoices.Symbols(doc, BuiltInCategory.OST_TitleBlocks, false), s.Pranchas.Carimbo);
            if (!dlg.Run()) return Result.Cancelled;

            List<Level> levels = f.Checked<Level>("niveis");
            List<PlantaTecnicaDef> defs = f.Checked<PlantaTecnicaDef>("plantas");
            if (levels.Count == 0 || defs.Count == 0) throw new UserMessageException("Selecione ao menos um nível e uma planta.");

            var report = new Report(Title);
            var vt = new ViewTools(doc);
            var sheets = new SheetService(doc, report, s.Pranchas);
            FamilySymbol tb = f.Bool("prancha") ? sheets.TitleBlock(f.String("carimbo")) : null;
            DimensionType dimType = Q.DimensionType(doc, Choices.Value(f.String("tipo")));
            ViewFamilyType floorType = vt.FamilyType(ViewFamily.FloorPlan);
            ViewFamilyType ceilingType = vt.FamilyType(ViewFamily.CeilingPlan);

            using (var group = new TransactionGroup(doc, "DetalhaBIM - " + Title))
            {
                group.Start();
                foreach (Level level in levels)
                {
                    foreach (PlantaTecnicaDef def in defs)
                    {
                        string name = $"{def.Nome} - {level.Name}";
                        if (f.Bool("pular") && vt.NameExists(name))
                        {
                            report.Count("plantas já existentes (mantidas)");
                            continue;
                        }
                        ViewFamilyType type = def.Tipo == "Forro" ? ceilingType : floorType;
                        if (type == null) continue;

                        try
                        {
                            Tx.Run(doc, name, () =>
                            {
                                ViewPlan v = ViewPlan.Create(doc, type.Id, level.Id);
                                vt.SetName(v, name);
                                ViewTools.SetScale(v, def.Escala);
                                vt.ApplyTemplate(v, def.Modelo, report);
                                doc.Regenerate();
                                report.Count("plantas criadas");
                                Automate(doc, v, def, dimType, s, report);
                                if (tb != null) report.Count("pranchas", sheets.Layout(new List<View> { v }, tb, v.Name).Count);
                            }, deleteFailingAnnotations: true);
                        }
                        catch (System.Exception ex)
                        {
                            report.Warn($"{name}: {ex.Message}");
                            Logger.Error("Planta técnica " + name, ex);
                        }
                    }
                }
                group.Assimilate();
            }

            report.Show();
            return Result.Succeeded;
        }

        private static string Describe(PlantaTecnicaDef p)
        {
            var parts = new List<string> { p.Tipo, "1:" + p.Escala };
            if (!string.IsNullOrWhiteSpace(p.Modelo)) parts.Add(p.Modelo);
            if (p.CotasExternas || p.CotasInternas) parts.Add("cotas");
            if (p.EtiquetarAmbientes || p.EtiquetarEsquadrias) parts.Add("etiquetas");
            return string.Join(" · ", parts);
        }

        private static void Automate(Document doc, ViewPlan v, PlantaTecnicaDef def, DimensionType dimType, DetalhaSettings s, Report report)
        {
            var tags = new TagService(doc, report);
            if (def.EtiquetarAmbientes) report.Count("etiquetas de ambiente", tags.TagRooms(v, null, true));
            if (def.EtiquetarEsquadrias)
            {
                report.Count("etiquetas de portas", tags.TagCategory(v, BuiltInCategory.OST_Doors, null, true, 4));
                report.Count("etiquetas de janelas", tags.TagCategory(v, BuiltInCategory.OST_Windows, null, true, 4));
            }

            var builder = new DimensionBuilder(doc, v, dimType, Conv.Cm(s.Cotas.MenorSegmentoCm));
            if (def.CotasExternas)
            {
                var facade = new FacadeDimensionService(doc, v, builder, report);
                report.Count("cotas externas", facade.DimensionAllSides(new FacadeOptions
                {
                    FirstOffset = Conv.PaperMm(s.Cotas.PrimeiraLinhaMm + 4, v),
                    Spacing = Conv.PaperMm(s.Cotas.EspacamentoMm, v),
                }));
            }
            if (def.CotasInternas)
            {
                var rooms = new RoomDimensionService(doc, v, builder, report);
                var options = new RoomDimensionOptions { AtCenter = true, Openings = false };
                foreach (Room room in Q.Rooms(doc).Where(r => r.LevelId == v.GenLevel.Id && RoomGeo.IsPlaced(r)))
                {
                    report.Count("cotas internas", rooms.Dimension(room, options));
                }
            }
        }
    }
}
