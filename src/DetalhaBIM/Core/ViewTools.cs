using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    /// <summary>Criação e ajuste de vistas: tipos, modelos, nomes únicos e recortes.</summary>
    public class ViewTools
    {
        private readonly Document _doc;
        private HashSet<string> _names;

        public ViewTools(Document doc)
        {
            _doc = doc;
        }

        public ViewFamilyType FamilyType(ViewFamily family)
        {
            return Q.All<ViewFamilyType>(_doc).FirstOrDefault(t => t.ViewFamily == family);
        }

        public List<View> Templates()
        {
            return Q.All<View>(_doc).Where(v => v.IsTemplate).OrderBy(v => v.Name).ToList();
        }

        public View Template(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return Templates().FirstOrDefault(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Gera um nome ainda não usado por nenhuma vista, acrescentando (2), (3)...</summary>
        public string UniqueName(string baseName)
        {
            if (_names == null)
            {
                _names = new HashSet<string>(Q.All<View>(_doc).Select(v => v.Name), StringComparer.OrdinalIgnoreCase);
            }
            string clean = Sanitize(baseName);
            string name = clean;
            int i = 2;
            while (_names.Contains(name)) name = $"{clean} ({i++})";
            _names.Add(name);
            return name;
        }

        public bool NameExists(string name)
        {
            if (_names == null) UniqueName("__init__");
            return _names.Contains(Sanitize(name));
        }

        public void SetName(View v, string baseName)
        {
            try
            {
                v.Name = UniqueName(baseName);
            }
            catch
            {
                // Mantém o nome automático do Revit se o nome for recusado.
            }
        }

        public static string Sanitize(string name)
        {
            char[] invalid = { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };
            string s = new string((name ?? "Vista").Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
            return string.IsNullOrEmpty(s) ? "Vista" : s;
        }

        public void ApplyTemplate(View view, string templateName, Report report)
        {
            if (string.IsNullOrWhiteSpace(templateName)) return;
            View template = Template(templateName);
            if (template == null)
            {
                report?.Warn($"Modelo de vista \"{templateName}\" não existe neste projeto — vistas criadas sem modelo.");
                return;
            }
            try
            {
                view.ViewTemplateId = template.Id;
            }
            catch
            {
                report?.Warn($"O modelo \"{templateName}\" não é compatível com a vista {view.Name}.");
            }
        }

        public static void SetScale(View view, int scale)
        {
            if (scale <= 0) return;
            try
            {
                view.Scale = scale;
            }
            catch
            {
                // A escala pode ser controlada pelo modelo de vista.
            }
        }

        /// <summary>
        /// Ajusta o recorte (crop) da vista para envolver os pontos informados, com folga.
        /// Funciona em plantas e elevações; a profundidade (Z do recorte) é preservada.
        /// </summary>
        public static void CropToPoints(View view, IEnumerable<XYZ> points, double margin, double marginBottom = -1)
        {
            BoundingBoxXYZ bb = view.CropBox;
            Transform inv = bb.Transform.Inverse;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (XYZ p in points)
            {
                XYZ l = inv.OfPoint(p);
                minX = Math.Min(minX, l.X);
                minY = Math.Min(minY, l.Y);
                maxX = Math.Max(maxX, l.X);
                maxY = Math.Max(maxY, l.Y);
            }
            if (minX > maxX) return;
            double mb = marginBottom < 0 ? margin : marginBottom;
            bb.Min = new XYZ(minX - margin, minY - mb, bb.Min.Z);
            bb.Max = new XYZ(maxX + margin, maxY + margin, bb.Max.Z);
            view.CropBoxActive = true;
            view.CropBox = bb;
            view.CropBoxVisible = false;
        }

        /// <summary>Planta de piso existente (não-modelo) para o nível.</summary>
        public ViewPlan FloorPlanOf(ElementId levelId, View preferred = null)
        {
            if (preferred is ViewPlan vp && !vp.IsTemplate && vp.GenLevel?.Id == levelId && vp.ViewType == ViewType.FloorPlan)
                return vp;
            return Q.All<ViewPlan>(_doc).FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan && v.GenLevel?.Id == levelId);
        }

        /// <summary>Vista 3D auxiliar para lançamento de raios (ReferenceIntersector).</summary>
        public View3D Aux3D()
        {
            const string auxName = "DetalhaBIM - 3D auxiliar";
            View3D existing = Q.All<View3D>(_doc).FirstOrDefault(v => !v.IsTemplate && !v.IsPerspective && v.Name == auxName);
            if (existing != null) return existing;
            ViewFamilyType vft = FamilyType(ViewFamily.ThreeDimensional);
            View3D v3 = View3D.CreateIsometric(_doc, vft.Id);
            try
            {
                v3.Name = auxName;
            }
            catch
            {
                // Nome já existente: mantém o automático.
            }
            return v3;
        }

        /// <summary>Plano de corte da vista em cota absoluta (padrão: nível + 1,20 m).</summary>
        public static double CutElevation(View view)
        {
            double level = view.GenLevel?.Elevation ?? 0;
            if (view is ViewPlan vp)
            {
                try
                {
                    PlanViewRange range = vp.GetViewRange();
                    ElementId cutLevelId = range.GetLevelId(PlanViewPlane.CutPlane);
                    double baseElev = (vp.Document.GetElement(cutLevelId) as Level)?.Elevation ?? level;
                    return baseElev + range.GetOffset(PlanViewPlane.CutPlane);
                }
                catch
                {
                    // usa o padrão
                }
            }
            return level + Conv.M(1.2);
        }
    }
}
