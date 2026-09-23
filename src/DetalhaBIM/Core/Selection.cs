using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace DetalhaBIM.Core
{
    /// <summary>Filtro de seleção genérico por predicado.</summary>
    public class PredicateFilter : ISelectionFilter
    {
        private readonly Func<Element, bool> _predicate;

        public PredicateFilter(Func<Element, bool> predicate)
        {
            _predicate = predicate;
        }

        public bool AllowElement(Element elem) => elem != null && _predicate(elem);

        public bool AllowReference(Reference reference, XYZ position) => false;

        public static PredicateFilter Categories(params BuiltInCategory[] cats)
        {
            var set = new HashSet<BuiltInCategory>(cats);
            return new PredicateFilter(e => e.Category != null && set.Contains(e.Category.BuiltInCategory));
        }

        public static PredicateFilter Rooms() => new PredicateFilter(e => e is Room || e is RoomTag);
    }

    public enum RoomScope
    {
        Selecionar = 0,
        VistaAtiva = 1,
        Nivel = 2,
        Projeto = 3,
    }

    public static class Pick
    {
        public static readonly string[] RoomScopeOptions =
        {
            "Selecionar ambientes na vista",
            "Todos os ambientes visíveis na vista ativa",
            "Todos os ambientes do nível da vista ativa",
            "Todos os ambientes do projeto",
        };

        /// <summary>
        /// Obtém os ambientes conforme o escopo. No modo "Selecionar", usa a seleção atual
        /// (ambientes ou etiquetas de ambiente) ou solicita a seleção na tela.
        /// </summary>
        public static List<Room> Rooms(UIDocument uidoc, RoomScope scope)
        {
            Document doc = uidoc.Document;
            View view = uidoc.ActiveView;
            IEnumerable<Room> rooms;

            switch (scope)
            {
                case RoomScope.VistaAtiva:
                    rooms = new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType().OfType<Room>();
                    break;
                case RoomScope.Nivel:
                    ElementId levelId = view.GenLevel?.Id ?? throw new UserMessageException("A vista ativa não está associada a um nível. Abra uma planta.");
                    rooms = Q.Rooms(doc).Where(r => r.LevelId == levelId);
                    break;
                case RoomScope.Projeto:
                    rooms = Q.Rooms(doc);
                    break;
                default:
                    rooms = SelectedOrPick(uidoc, PredicateFilter.Rooms(), "Selecione os ambientes (ou suas etiquetas) e clique em Concluir")
                        .Select(e => e is RoomTag t ? t.Room : e as Room);
                    break;
            }

            List<Room> list = rooms.Where(RoomGeo.IsPlaced)
                .GroupBy(r => r.Id).Select(g => g.First())
                .OrderBy(r => r.Level?.Elevation ?? 0).ThenBy(r => RoomGeo.Number(r), NaturalComparer.Instance)
                .ToList();
            if (list.Count == 0) throw new UserMessageException("Nenhum ambiente delimitado foi encontrado para o escopo escolhido.");
            return list;
        }

        /// <summary>Usa a seleção atual filtrada; se vazia, pede para o usuário selecionar.</summary>
        public static List<Element> SelectedOrPick(UIDocument uidoc, ISelectionFilter filter, string prompt)
        {
            Document doc = uidoc.Document;
            List<Element> selected = uidoc.Selection.GetElementIds().Select(doc.GetElement)
                .Where(e => e != null && filter.AllowElement(e)).ToList();
            if (selected.Count > 0) return selected;

            IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.Element, filter, prompt);
            return refs.Select(r => doc.GetElement(r)).Where(e => e != null).ToList();
        }
    }

    /// <summary>Ordena textos com números de forma natural (2 antes de 10).</summary>
    public class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new NaturalComparer();

        public int Compare(string x, string y)
        {
            x ??= string.Empty;
            y ??= string.Empty;
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    int si = i, sj = j;
                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;
                    string nx = x.Substring(si, i - si).TrimStart('0'), ny = y.Substring(sj, j - sj).TrimStart('0');
                    int c = nx.Length != ny.Length ? nx.Length.CompareTo(ny.Length) : string.CompareOrdinal(nx, ny);
                    if (c != 0) return c;
                }
                else
                {
                    int c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                    if (c != 0) return c;
                    i++;
                    j++;
                }
            }
            return (x.Length - i).CompareTo(y.Length - j);
        }
    }

    /// <summary>Ordem de leitura de prancha: de cima para baixo, da esquerda para a direita.</summary>
    public static class ReadingOrder
    {
        public static List<T> Sort<T>(IEnumerable<T> items, Func<T, XYZ> point, XYZ right, XYZ up, double rowTolerance)
        {
            var data = items.Select(i => new { Item = i, P = point(i) })
                .Where(a => a.P != null)
                .Select(a => new { a.Item, X = a.P.DotProduct(right), Y = a.P.DotProduct(up) })
                .OrderByDescending(a => a.Y)
                .ToList();

            var result = new List<T>();
            int k = 0;
            while (k < data.Count)
            {
                double top = data[k].Y;
                var row = new List<(T item, double x)>();
                while (k < data.Count && top - data[k].Y <= rowTolerance)
                {
                    row.Add((data[k].Item, data[k].X));
                    k++;
                }
                result.AddRange(row.OrderBy(r => r.x).Select(r => r.item));
            }
            return result;
        }
    }
}
