using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Documentacao
{
    /// <summary>
    /// Quadros prontos: esquadrias (portas e janelas agrupadas por código, com dimensões,
    /// peitoril e quantidade), áreas e acabamentos dos ambientes.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class QuadrosCommand : CommandBase
    {
        protected override string Title => "Quadros e Tabelas";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            var dlg = new OptionsDialog("quadros", Title,
                "Cria tabelas prontas para prancha, com campos, cabeçalhos em português, agrupamentos e totais já configurados.",
                Theme.Documentacao, ctx.MainWindow, "Criar quadros");
            FormBuilder f = dlg.Form;
            f.Section("Quadros");
            f.Check("portas", "Quadro de portas (código, largura, altura, descrição, quantidade)", true);
            f.Text("nPortas", "   Nome", "QUADRO DE PORTAS");
            f.Check("janelas", "Quadro de janelas (código, largura, altura, peitoril, descrição, quantidade)", true);
            f.Text("nJanelas", "   Nome", "QUADRO DE JANELAS");
            f.Check("areas", "Quadro de áreas dos ambientes (com total)", true);
            f.Text("nAreas", "   Nome", "QUADRO DE ÁREAS");
            f.Check("acabamentos", "Quadro de acabamentos dos ambientes", false);
            f.Text("nAcab", "   Nome", "QUADRO DE ACABAMENTOS");
            f.Section("Opções");
            f.Radio("existente", "Se já existir um quadro com o mesmo nome", new[] { "Criar outro com nome numerado", "Substituir (remove o anterior, inclusive das pranchas)" }, 0);
            f.Check("abrir", "Abrir o primeiro quadro criado", true);
            if (!dlg.Run()) return Result.Cancelled;

            var report = new Report(Title);
            bool replace = f.Index("existente") == 1;
            ViewSchedule first = null;

            Tx.Run(doc, Title, () =>
            {
                if (f.Bool("portas")) Keep(ref first, Openings(doc, BuiltInCategory.OST_Doors, f.String("nPortas"), false, replace, report));
                if (f.Bool("janelas")) Keep(ref first, Openings(doc, BuiltInCategory.OST_Windows, f.String("nJanelas"), true, replace, report));
                if (f.Bool("areas")) Keep(ref first, Areas(doc, f.String("nAreas"), replace, report));
                if (f.Bool("acabamentos")) Keep(ref first, Finishes(doc, f.String("nAcab"), replace, report));
            });

            report.Show();
            if (f.Bool("abrir") && first != null)
            {
                try
                {
                    ctx.UiDoc.ActiveView = first;
                }
                catch
                {
                    // Revit pode recusar trocar de vista neste momento.
                }
            }
            return Result.Succeeded;
        }

        private static void Keep(ref ViewSchedule first, ViewSchedule created)
        {
            if (first == null) first = created;
        }

        private static ViewSchedule Openings(Document doc, BuiltInCategory cat, string name, bool sill, bool replace, Report report)
        {
            ViewSchedule vs = Create(doc, cat, name, replace);
            var b = new FieldAdder(doc, vs);
            ScheduleField code = b.Add("CÓDIGO", new[] { BuiltInParameter.ALL_MODEL_TYPE_MARK }, "Marca de tipo", "Type Mark");
            ScheduleField w = b.Add("LARGURA (m)", new[] { cat == BuiltInCategory.OST_Doors ? BuiltInParameter.DOOR_WIDTH : BuiltInParameter.WINDOW_WIDTH, BuiltInParameter.FAMILY_WIDTH_PARAM }, "Largura", "Width");
            ScheduleField h = b.Add("ALTURA (m)", new[] { cat == BuiltInCategory.OST_Doors ? BuiltInParameter.DOOR_HEIGHT : BuiltInParameter.WINDOW_HEIGHT, BuiltInParameter.FAMILY_HEIGHT_PARAM }, "Altura", "Height");
            ScheduleField p = sill ? b.Add("PEITORIL (m)", new[] { BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM }, "Altura do peitoril", "Sill Height") : null;
            b.Add("DESCRIÇÃO", new[] { BuiltInParameter.ALL_MODEL_DESCRIPTION }, "Descrição", "Description");
            ScheduleField count = b.Count("QTD.");

            Meters(w);
            Meters(h);
            Meters(p);
            ScheduleDefinition def = vs.Definition;
            def.IsItemized = false;
            if (code != null) def.AddSortGroupField(new ScheduleSortGroupField(code.FieldId));
            if (p != null) def.AddSortGroupField(new ScheduleSortGroupField(p.FieldId));
            if (count != null) def.ShowGrandTotal = true;
            TrySet(() => def.ShowGrandTotalCount = true);
            TrySet(() => def.ShowGrandTotalTitle = true);

            report.Count("quadros de esquadrias");
            if (b.Missing.Count > 0) report.Warn($"{vs.Name}: campos não encontrados — {string.Join(", ", b.Missing)}.");
            return vs;
        }

        private static ViewSchedule Areas(Document doc, string name, bool replace, Report report)
        {
            ViewSchedule vs = Create(doc, BuiltInCategory.OST_Rooms, name, replace);
            var b = new FieldAdder(doc, vs);
            ScheduleField level = b.Add("PAVIMENTO", new[] { BuiltInParameter.ROOM_LEVEL_ID }, "Nível", "Level");
            ScheduleField number = b.Add("Nº", new[] { BuiltInParameter.ROOM_NUMBER }, "Número", "Number");
            b.Add("AMBIENTE", new[] { BuiltInParameter.ROOM_NAME }, "Nome", "Name");
            ScheduleField area = b.Add("ÁREA (m²)", new[] { BuiltInParameter.ROOM_AREA }, "Área", "Area");
            ScheduleField per = b.Add("PERÍMETRO (m)", new[] { BuiltInParameter.ROOM_PERIMETER }, "Perímetro", "Perimeter");

            Format(area, UnitTypeId.SquareMeters, 0.01);
            Meters(per);
            ScheduleDefinition def = vs.Definition;
            def.IsItemized = true;
            if (level != null)
            {
                var g = new ScheduleSortGroupField(level.FieldId) { ShowHeader = true, ShowFooter = true };
                def.AddSortGroupField(g);
            }
            if (number != null) def.AddSortGroupField(new ScheduleSortGroupField(number.FieldId));
            if (area != null)
            {
                TrySet(() => area.DisplayType = ScheduleFieldDisplayType.Totals);
                TrySet(() => def.AddFilter(new ScheduleFilter(area.FieldId, ScheduleFilterType.GreaterThan, 0.0)));
            }
            def.ShowGrandTotal = true;
            TrySet(() => def.ShowGrandTotalTitle = true);

            report.Count("quadros de áreas");
            return vs;
        }

        private static ViewSchedule Finishes(Document doc, string name, bool replace, Report report)
        {
            ViewSchedule vs = Create(doc, BuiltInCategory.OST_Rooms, name, replace);
            var b = new FieldAdder(doc, vs);
            ScheduleField number = b.Add("Nº", new[] { BuiltInParameter.ROOM_NUMBER }, "Número", "Number");
            b.Add("AMBIENTE", new[] { BuiltInParameter.ROOM_NAME }, "Nome", "Name");
            b.Add("PISO", new[] { BuiltInParameter.ROOM_FINISH_FLOOR }, "Acabamento do piso", "Floor Finish");
            b.Add("RODAPÉ", new[] { BuiltInParameter.ROOM_FINISH_BASE }, "Acabamento da base", "Base Finish");
            b.Add("PAREDES", new[] { BuiltInParameter.ROOM_FINISH_WALL }, "Acabamento da parede", "Wall Finish");
            b.Add("FORRO", new[] { BuiltInParameter.ROOM_FINISH_CEILING }, "Acabamento do forro", "Ceiling Finish");
            ScheduleDefinition def = vs.Definition;
            def.IsItemized = true;
            if (number != null) def.AddSortGroupField(new ScheduleSortGroupField(number.FieldId));
            report.Count("quadros de acabamentos");
            return vs;
        }

        private static ViewSchedule Create(Document doc, BuiltInCategory cat, string name, bool replace)
        {
            string clean = ViewTools.Sanitize(string.IsNullOrWhiteSpace(name) ? "QUADRO" : name.Trim());
            if (replace)
            {
                foreach (ViewSchedule old in Q.All<ViewSchedule>(doc).Where(s => s.Name.Equals(clean, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    doc.Delete(old.Id);
                }
            }
            ViewSchedule vs = ViewSchedule.CreateSchedule(doc, new ElementId(cat));
            new ViewTools(doc).SetName(vs, clean);
            return vs;
        }

        private static void Meters(ScheduleField f) => Format(f, UnitTypeId.Meters, 0.01);

        private static void Format(ScheduleField f, ForgeTypeId unit, double accuracy)
        {
            if (f == null) return;
            TrySet(() =>
            {
                var fo = new FormatOptions(unit) { UseDefault = false, Accuracy = accuracy };
                f.SetFormatOptions(fo);
            });
        }

        private static void TrySet(Action a)
        {
            try
            {
                a();
            }
            catch
            {
                // Recurso não disponível para este campo/categoria.
            }
        }

        /// <summary>Adiciona campos procurando por parâmetro interno e, em seguida, pelo nome.</summary>
        private class FieldAdder
        {
            private readonly Document _doc;
            private readonly ScheduleDefinition _def;
            private readonly IList<SchedulableField> _fields;

            public FieldAdder(Document doc, ViewSchedule vs)
            {
                _doc = doc;
                _def = vs.Definition;
                _fields = _def.GetSchedulableFields();
            }

            public List<string> Missing { get; } = new List<string>();

            public ScheduleField Add(string heading, BuiltInParameter[] bips, params string[] names)
            {
                SchedulableField sf = null;
                foreach (BuiltInParameter bip in bips)
                {
                    var id = new ElementId(bip);
                    sf = _fields.FirstOrDefault(x => x.ParameterId == id);
                    if (sf != null) break;
                }
                sf ??= _fields.FirstOrDefault(x => names.Any(n => string.Equals(x.GetName(_doc), n, StringComparison.OrdinalIgnoreCase)));
                if (sf == null)
                {
                    Missing.Add(heading);
                    return null;
                }
                ScheduleField field = _def.AddField(sf);
                field.ColumnHeading = heading;
                return field;
            }

            public ScheduleField Count(string heading)
            {
                SchedulableField sf = _fields.FirstOrDefault(x => x.FieldType == ScheduleFieldType.Count);
                if (sf == null) return null;
                ScheduleField field = _def.AddField(sf);
                field.ColumnHeading = heading;
                return field;
            }
        }
    }
}
