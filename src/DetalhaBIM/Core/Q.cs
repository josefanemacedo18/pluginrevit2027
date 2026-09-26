using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace DetalhaBIM.Core
{
    /// <summary>Consultas frequentes ao modelo (coletores, tipos, parâmetros).</summary>
    public static class Q
    {
        public static List<T> All<T>(Document doc) where T : Element
        {
            return new FilteredElementCollector(doc).OfClass(typeof(T)).Cast<T>().ToList();
        }

        public static List<Element> InView(Document doc, View view, BuiltInCategory bic)
        {
            return new FilteredElementCollector(doc, view.Id).OfCategory(bic).WhereElementIsNotElementType().ToList();
        }

        public static List<Wall> WallsInView(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id).OfClass(typeof(Wall)).Cast<Wall>()
                .Where(w => w.Location is LocationCurve).ToList();
        }

        public static List<Grid> GridsInView(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid)).Cast<Grid>().ToList();
        }

        public static List<Level> Levels(Document doc)
        {
            return All<Level>(doc).OrderBy(l => l.Elevation).ToList();
        }

        public static List<Room> Rooms(Document doc)
        {
            return new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType().OfType<Room>().ToList();
        }

        public static List<FamilySymbol> Symbols(Document doc, BuiltInCategory bic)
        {
            return new FilteredElementCollector(doc).OfCategory(bic).OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>().OrderBy(s => s.FamilyName).ThenBy(s => s.Name).ToList();
        }

        public static List<ElementType> Types(Document doc, Type cls)
        {
            return new FilteredElementCollector(doc).OfClass(cls).Cast<ElementType>()
                .OrderBy(t => t.Name).ToList();
        }

        public static string Label(ElementType t)
        {
            if (t == null) return string.Empty;
            return string.IsNullOrEmpty(t.FamilyName) ? t.Name : t.FamilyName + " : " + t.Name;
        }

        public static List<DimensionType> LinearDimensionTypes(Document doc)
        {
            return All<DimensionType>(doc)
                .Where(t => t.StyleType == DimensionStyleType.Linear && !string.IsNullOrWhiteSpace(t.Name))
                .OrderBy(t => t.Name).ToList();
        }

        public static List<SpotDimensionType> SpotElevationTypes(Document doc)
        {
            return All<SpotDimensionType>(doc)
                .Where(t => t.StyleType == DimensionStyleType.SpotElevation && !string.IsNullOrWhiteSpace(t.Name))
                .OrderBy(t => t.Name).ToList();
        }

        public static DimensionType DimensionType(Document doc, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return LinearDimensionTypes(doc).FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Encontra um tipo pelo rótulo "Família : Tipo" ou apenas pelo nome.</summary>
        public static T TypeByLabel<T>(IEnumerable<T> types, string label) where T : ElementType
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            return types.FirstOrDefault(t => Label(t).Equals(label, StringComparison.OrdinalIgnoreCase))
                   ?? types.FirstOrDefault(t => t.Name.Equals(label, StringComparison.OrdinalIgnoreCase));
        }

        public static FamilySymbol SymbolByLabel(Document doc, BuiltInCategory bic, string label)
        {
            return TypeByLabel(Symbols(doc, bic), label);
        }

        public static List<ElementType> SweepTypes(Document doc)
        {
            return new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Cornices).WhereElementIsElementType()
                .Cast<ElementType>().OrderBy(Label).ToList();
        }

        /// <summary>
        /// Material pelo nome. Se <paramref name="create"/> for verdadeiro e ele não existir, cria um
        /// material novo com esse nome (deve ser chamado dentro de uma transação).
        /// </summary>
        public static Material Material(Document doc, string name, bool create, Report report = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            Material m = All<Material>(doc).FirstOrDefault(x => x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (m != null || !create) return m;
            ElementId id = Autodesk.Revit.DB.Material.Create(doc, name.Trim());
            report?.Count("materiais criados");
            return doc.GetElement(id) as Material;
        }

        // ------------------------------------------------------------------ parâmetros

        public static double? Double(Element e, BuiltInParameter bip)
        {
            Parameter p = e?.get_Parameter(bip);
            if (p == null || !p.HasValue || p.StorageType != StorageType.Double) return null;
            return p.AsDouble();
        }

        public static string Text(Element e, BuiltInParameter bip)
        {
            Parameter p = e?.get_Parameter(bip);
            if (p == null || !p.HasValue) return string.Empty;
            return p.StorageType == StorageType.String ? p.AsString() ?? string.Empty : p.AsValueString() ?? string.Empty;
        }

        public static bool Set(Element e, BuiltInParameter bip, double value)
        {
            Parameter p = e?.get_Parameter(bip);
            if (p == null || p.IsReadOnly || p.StorageType != StorageType.Double) return false;
            return p.Set(value);
        }

        public static bool Set(Element e, BuiltInParameter bip, string value)
        {
            Parameter p = e?.get_Parameter(bip);
            if (p == null || p.IsReadOnly || p.StorageType != StorageType.String) return false;
            return p.Set(value);
        }

        public static bool Set(Element e, BuiltInParameter bip, int value)
        {
            Parameter p = e?.get_Parameter(bip);
            if (p == null || p.IsReadOnly || p.StorageType != StorageType.Integer) return false;
            return p.Set(value);
        }

        /// <summary>Primeiro valor de comprimento encontrado na instância ou no tipo.</summary>
        public static double? FirstLength(Element e, params BuiltInParameter[] bips)
        {
            Element type = e.Document.GetElement(e.GetTypeId());
            foreach (BuiltInParameter bip in bips)
            {
                double? v = Double(e, bip);
                if (v.HasValue && v.Value > Geo.Eps) return v;
                v = Double(type, bip);
                if (v.HasValue && v.Value > Geo.Eps) return v;
            }
            return null;
        }

        public static double? OpeningWidth(FamilyInstance fi) =>
            FirstLength(fi, BuiltInParameter.FAMILY_WIDTH_PARAM, BuiltInParameter.DOOR_WIDTH, BuiltInParameter.WINDOW_WIDTH, BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM);

        public static double? OpeningHeight(FamilyInstance fi) =>
            FirstLength(fi, BuiltInParameter.FAMILY_HEIGHT_PARAM, BuiltInParameter.DOOR_HEIGHT, BuiltInParameter.WINDOW_HEIGHT, BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM);

        public static bool IsOpening(Element e)
        {
            BuiltInCategory? bic = e?.Category?.BuiltInCategory;
            return bic == BuiltInCategory.OST_Doors || bic == BuiltInCategory.OST_Windows;
        }

        /// <summary>Portas e janelas hospedadas nas paredes informadas.</summary>
        public static List<FamilyInstance> OpeningsIn(Document doc, ICollection<ElementId> wallIds)
        {
            var cats = new ElementMulticategoryFilter(new[] { BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows });
            return new FilteredElementCollector(doc).WherePasses(cats).OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Host != null && wallIds.Contains(fi.Host.Id))
                .ToList();
        }

        /// <summary>
        /// Linha central real da parede. A "Linha de localização" do Revit pode estar na face de
        /// acabamento ou no núcleo; aqui ela é deslocada para o meio da espessura, medido nas
        /// faces laterais da própria parede.
        /// </summary>
        public static Line WallCenterline(Wall w)
        {
            if (!(w.Location is LocationCurve lc) || !(lc.Curve is Line line)) return null;
            int key = w.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM)?.AsInteger() ?? 0;
            if (key == 0) return line;

            XYZ o = Geo.FlatDir(w.Orientation);
            if (o.IsZeroLength()) return line;
            double loc = line.Origin.DotProduct(o);
            double center;
            double? ext = SideFacePosition(w, ShellLayerType.Exterior, o);
            double? inn = SideFacePosition(w, ShellLayerType.Interior, o);
            if (ext.HasValue && inn.HasValue)
            {
                center = (ext.Value + inn.Value) / 2;
            }
            else
            {
                // 2 = face de acabamento externa, 3 = face de acabamento interna (demais: aproximação no eixo).
                center = key == 2 ? loc - w.Width / 2 : key == 3 ? loc + w.Width / 2 : loc;
            }
            if (Math.Abs(center - loc) < 1e-9) return line;
            return (Line)line.CreateTransformed(Transform.CreateTranslation(o * (center - loc)));
        }

        private static double? SideFacePosition(Wall w, ShellLayerType side, XYZ o)
        {
            try
            {
                foreach (Reference r in HostObjectUtils.GetSideFaces(w, side))
                {
                    if (w.GetGeometryObjectFromReference(r) is PlanarFace pf) return pf.Origin.DotProduct(o);
                }
            }
            catch
            {
                // Paredes sem faces laterais acessíveis (ex.: cortina).
            }
            return null;
        }

        public static Level Level(Document doc, Element e)
        {
            if (e is View v && v.GenLevel != null) return v.GenLevel;
            ElementId id = e.LevelId;
            if (id == ElementId.InvalidElementId && e is FamilyInstance fi && fi.Host != null) id = fi.Host.LevelId;
            return id == ElementId.InvalidElementId ? null : doc.GetElement(id) as Level;
        }
    }
}
