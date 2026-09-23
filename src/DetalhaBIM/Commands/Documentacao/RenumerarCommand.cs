using System;
using System.Collections.Generic;
using System.Linq;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DetalhaBIM.Core;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Documentacao
{
    /// <summary>
    /// Renumeração de elementos por Marca ou Marca de tipo: automática (leitura da planta — de
    /// cima para baixo, da esquerda para a direita) ou manual (clicando na ordem desejada).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RenumerarCommand : CommandBase
    {
        protected override string Title => "Renumerar Elementos";

        private class Cat
        {
            public string Name;
            public BuiltInCategory Bic;
            public Func<RenumeracaoSettings, string> Prefix;
        }

        private static readonly List<Cat> Cats = new List<Cat>
        {
            new Cat { Name = "Portas", Bic = BuiltInCategory.OST_Doors, Prefix = s => s.PrefixoPortas },
            new Cat { Name = "Janelas", Bic = BuiltInCategory.OST_Windows, Prefix = s => s.PrefixoJanelas },
            new Cat { Name = "Ambientes", Bic = BuiltInCategory.OST_Rooms, Prefix = s => s.PrefixoAmbientes },
            new Cat { Name = "Pilares estruturais", Bic = BuiltInCategory.OST_StructuralColumns, Prefix = s => s.PrefixoPilares },
            new Cat { Name = "Pilares arquitetônicos", Bic = BuiltInCategory.OST_Columns, Prefix = s => s.PrefixoPilares },
            new Cat { Name = "Vagas de estacionamento", Bic = BuiltInCategory.OST_Parking, Prefix = s => "V" },
            new Cat { Name = "Mobiliário", Bic = BuiltInCategory.OST_Furniture, Prefix = s => s.PrefixoOutros },
            new Cat { Name = "Peças sanitárias", Bic = BuiltInCategory.OST_PlumbingFixtures, Prefix = s => s.PrefixoOutros },
            new Cat { Name = "Luminárias", Bic = BuiltInCategory.OST_LightingFixtures, Prefix = s => s.PrefixoOutros },
            new Cat { Name = "Modelos genéricos", Bic = BuiltInCategory.OST_GenericModel, Prefix = s => s.PrefixoOutros },
        };

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            RenumeracaoSettings rs = ctx.Settings.Renumeracao;

            var dlg = new OptionsDialog("renumerar", Title,
                "Numera elementos na ordem de leitura da prancha (de cima para baixo, da esquerda para a direita) ou na ordem em que você clicar.",
                Theme.Documentacao, ctx.MainWindow, "Renumerar");
            FormBuilder f = dlg.Form;
            f.Section("O que numerar");
            WpfComboBox cat = f.Combo("categoria", "Categoria", Cats.Select(c => c.Name).ToList(), "Portas");
            f.Radio("parametro", "Parâmetro", new[] { "Marca (cada elemento recebe um número)", "Marca de tipo (cada tipo recebe um código — ex.: P01, J01)" }, 1);
            f.Hint("Em ambientes, o parâmetro utilizado é sempre o Número do ambiente.");
            f.Section("Ordem");
            f.Radio("modo", null, new[] { "Automática — de cima para baixo, da esquerda para a direita", "Manual — clicar nos elementos na ordem desejada" }, 0);
            f.Radio("escopo", "Elementos (modo automático)", new[] { "Visíveis na vista ativa", "Selecionados", "Todo o projeto (nível por nível)" }, 0);
            f.Radio("ordemTipos", "Ordem dos tipos (Marca de tipo)", new[] { "Ordem de leitura da planta", "Tamanho (largura × altura)" }, 0);
            f.Number("tolerancia", "Tolerância de linha de leitura", rs.ToleranciaLinhaCm, "cm", "Elementos com diferença vertical menor que isto são considerados na mesma linha.");
            f.Section("Formato");
            WpfTextBox prefix = f.Text("prefixo", "Prefixo", rs.PrefixoPortas);
            f.Text("sufixo", "Sufixo", "");
            f.Number("inicio", "Número inicial", 1);
            f.Number("digitos", "Dígitos (01, 001...)", rs.Digitos);
            f.Number("incremento", "Incremento", 1);
            cat.SelectionChanged += (s, e) =>
            {
                Cat c = Cats.FirstOrDefault(x => x.Name == cat.SelectedItem as string);
                if (c != null) prefix.Text = c.Prefix(rs) ?? string.Empty;
            };
            if (!dlg.Run()) return Result.Cancelled;

            Cat category = Cats.First(c => c.Name == f.String("categoria"));
            bool isRoom = category.Bic == BuiltInCategory.OST_Rooms;
            bool typeMark = !isRoom && f.Index("parametro") == 1;
            var fmt = new Formatter(f.String("prefixo"), f.String("sufixo"), f.Int("inicio"), f.Int("digitos"), Math.Max(1, f.Int("incremento")));
            var report = new Report(Title);

            if (f.Index("modo") == 1)
            {
                Manual(ctx, category, isRoom, typeMark, fmt, report);
            }
            else
            {
                List<Element> elements = Collect(ctx, category, f.Index("escopo"));
                if (elements.Count == 0) throw new UserMessageException($"Nenhum elemento da categoria {category.Name} foi encontrado no escopo escolhido.");
                double tol = Conv.Cm(Math.Max(1, f.Double("tolerancia")));
                List<Element> ordered = Order(ctx, elements, tol);
                Tx.Run(doc, Title, () =>
                {
                    if (typeMark) NumberTypes(doc, ordered, f.Index("ordemTipos") == 1, fmt, report);
                    else NumberInstances(ordered, isRoom, fmt, report);
                });
            }

            report.Show();
            return Result.Succeeded;
        }

        private class Formatter
        {
            private readonly string _prefix, _suffix;
            private readonly int _digits, _step;
            private int _next;

            public Formatter(string prefix, string suffix, int start, int digits, int step)
            {
                _prefix = prefix ?? string.Empty;
                _suffix = suffix ?? string.Empty;
                _next = start;
                _digits = Math.Max(1, digits);
                _step = step;
            }

            public string Peek() => _prefix + _next.ToString().PadLeft(_digits, '0') + _suffix;

            public string Next()
            {
                string s = Peek();
                _next += _step;
                return s;
            }
        }

        private static List<Element> Collect(CommandContext ctx, Cat c, int scope)
        {
            Document doc = ctx.Doc;
            IEnumerable<Element> items;
            switch (scope)
            {
                case 1:
                    items = ctx.UiDoc.Selection.GetElementIds().Select(doc.GetElement)
                        .Where(e => e?.Category != null && e.Category.BuiltInCategory == c.Bic);
                    break;
                case 2:
                    items = new FilteredElementCollector(doc).OfCategory(c.Bic).WhereElementIsNotElementType();
                    break;
                default:
                    items = Q.InView(doc, ctx.ActiveView, c.Bic);
                    break;
            }
            return items.Where(e => !(e is Room r) || RoomGeo.IsPlaced(r))
                .Where(e => !(e is FamilyInstance fi) || fi.SuperComponent == null)
                .ToList();
        }

        /// <summary>Ordem de leitura por nível (do mais baixo ao mais alto) e, em cada nível, cima→baixo, esquerda→direita.</summary>
        private static List<Element> Order(CommandContext ctx, List<Element> elements, double tol)
        {
            View v = ctx.ActiveView;
            XYZ right = v is ViewPlan ? Geo.FlatDir(v.RightDirection) : XYZ.BasisX;
            XYZ up = v is ViewPlan ? Geo.FlatDir(v.UpDirection) : XYZ.BasisY;
            return elements
                .GroupBy(e => Q.Level(ctx.Doc, e)?.Elevation ?? 0)
                .OrderBy(g => g.Key)
                .SelectMany(g => ReadingOrder.Sort(g, e => Geo.ElementPoint(e), right, up, tol))
                .ToList();
        }

        private static void NumberInstances(List<Element> ordered, bool isRoom, Formatter fmt, Report report)
        {
            BuiltInParameter bip = isRoom ? BuiltInParameter.ROOM_NUMBER : BuiltInParameter.ALL_MODEL_MARK;
            foreach (Element e in ordered)
            {
                if (Q.Set(e, bip, fmt.Next())) report.Count("elementos numerados");
                else report.Warn($"Elemento {e.Id.Value}: parâmetro somente leitura.");
            }
        }

        private static void NumberTypes(Document doc, List<Element> ordered, bool bySize, Formatter fmt, Report report)
        {
            List<ElementType> types = ordered.Select(e => doc.GetElement(e.GetTypeId()) as ElementType)
                .Where(t => t != null).GroupBy(t => t.Id).Select(g => g.First()).ToList();
            if (bySize)
            {
                types = types.OrderBy(t => Size(t, BuiltInParameter.FAMILY_WIDTH_PARAM, BuiltInParameter.DOOR_WIDTH, BuiltInParameter.WINDOW_WIDTH))
                    .ThenBy(t => Size(t, BuiltInParameter.FAMILY_HEIGHT_PARAM, BuiltInParameter.DOOR_HEIGHT, BuiltInParameter.WINDOW_HEIGHT))
                    .ToList();
            }
            foreach (ElementType t in types)
            {
                if (Q.Set(t, BuiltInParameter.ALL_MODEL_TYPE_MARK, fmt.Next())) report.Count("tipos codificados");
            }
        }

        private static double Size(ElementType t, params BuiltInParameter[] bips)
        {
            foreach (BuiltInParameter b in bips)
            {
                double? v = Q.Double(t, b);
                if (v.HasValue && v > 0) return v.Value;
            }
            return 0;
        }

        private static void Manual(CommandContext ctx, Cat c, bool isRoom, bool typeMark, Formatter fmt, Report report)
        {
            Document doc = ctx.Doc;
            ISelectionFilter filter = c.Bic == BuiltInCategory.OST_Rooms
                ? PredicateFilter.Rooms()
                : PredicateFilter.Categories(c.Bic);
            var doneTypes = new HashSet<ElementId>();
            var done = new List<ElementId>();

            while (true)
            {
                Element e;
                try
                {
                    string what = typeMark ? "o tipo do elemento" : "o elemento";
                    Reference r = ctx.UiDoc.Selection.PickObject(ObjectType.Element, filter,
                        $"Clique para numerar {what} como \"{fmt.Peek()}\" (ESC para encerrar)");
                    e = doc.GetElement(r);
                    if (e is RoomTag tag) e = tag.Room;
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }
                if (e == null) continue;

                if (typeMark)
                {
                    ElementId typeId = e.GetTypeId();
                    if (!doneTypes.Add(typeId)) continue;
                    Tx.Run(doc, "Renumerar", () => Q.Set(doc.GetElement(typeId), BuiltInParameter.ALL_MODEL_TYPE_MARK, fmt.Next()));
                    report.Count("tipos codificados");
                }
                else
                {
                    if (done.Contains(e.Id)) continue;
                    Tx.Run(doc, "Renumerar", () => Q.Set(e, isRoom ? BuiltInParameter.ROOM_NUMBER : BuiltInParameter.ALL_MODEL_MARK, fmt.Next()));
                    report.Count("elementos numerados");
                }
                done.Add(e.Id);
                ctx.UiDoc.Selection.SetElementIds(done);
            }
            ctx.UiDoc.Selection.SetElementIds(new List<ElementId>());
        }
    }
}
