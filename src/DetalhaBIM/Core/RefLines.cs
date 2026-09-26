using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    /// <summary>
    /// Linhas de detalhe invisíveis usadas como referência de cota onde o modelo não oferece uma
    /// face paralela — por exemplo, a extremidade chanfrada de uma parede inclinada. Permitem
    /// cotas alinhadas em qualquer ângulo, medindo exatamente entre os pontos desejados.
    /// </summary>
    public class RefLines
    {
        private readonly Document _doc;
        private readonly View _view;
        private GraphicsStyle _style;
        private bool _searched;

        public RefLines(Document doc, View view)
        {
            _doc = doc;
            _view = view;
        }

        /// <summary>Vistas que aceitam linhas de detalhe (plantas, cortes, elevações).</summary>
        public static bool Supported(View v) => v is ViewPlan || v is ViewSection;

        public int Created { get; private set; }

        /// <summary>
        /// Cria uma linha curta, perpendicular a <paramref name="dir"/> no plano da vista, passando
        /// por <paramref name="p"/>, e a devolve como referência na posição informada.
        /// </summary>
        public RefPos Across(XYZ p, XYZ dir, double position, int priority = 4)
        {
            if (!Supported(_view)) return null;
            XYZ q = OnViewPlane(p);
            XYZ n = _view.ViewDirection.CrossProduct(dir);
            if (n.IsZeroLength()) return null;
            n = n.Normalize();
            double half = Conv.Mm(40);
            try
            {
                DetailCurve dc = _doc.Create.NewDetailCurve(_view, Line.CreateBound(q - n * half, q + n * half));
                if (dc == null) return null;
                GraphicsStyle style = InvisibleStyle();
                if (style != null)
                {
                    try
                    {
                        dc.LineStyle = style;
                    }
                    catch
                    {
                        // Mantém o estilo padrão se o invisível não for aceito.
                    }
                }
                Created++;
                Reference r = dc.GeometryCurve?.Reference;
                return r == null ? null : new RefPos(r, position, dc, priority);
            }
            catch
            {
                return null;
            }
        }

        private XYZ OnViewPlane(XYZ p)
        {
            if (_view is ViewPlan vp) return Geo.WithZ(p, vp.GenLevel?.Elevation ?? p.Z);
            XYZ n = _view.ViewDirection.Normalize();
            return p - n * (p - _view.Origin).DotProduct(n);
        }

        private GraphicsStyle InvisibleStyle()
        {
            if (_searched) return _style;
            _searched = true;
            _style = LineStyle(_doc, BuiltInCategory.OST_InvisibleLines, "<Invisible lines>", "<Linhas invisíveis>");
            return _style;
        }

        /// <summary>Estilo de linha pela categoria interna ou, na falta dela, pelo nome.</summary>
        public static GraphicsStyle LineStyle(Document doc, BuiltInCategory bic, params string[] names)
        {
            try
            {
                Category lines = Category.GetCategory(doc, BuiltInCategory.OST_Lines);
                if (lines == null) return null;
                foreach (Category sub in lines.SubCategories)
                {
                    bool match = sub.BuiltInCategory == bic
                                 || names.Any(n => string.Equals(sub.Name, n, StringComparison.OrdinalIgnoreCase));
                    if (match) return sub.GetGraphicsStyle(GraphicsStyleType.Projection);
                }
            }
            catch
            {
                // Sem estilos de linha acessíveis.
            }
            return null;
        }

        /// <summary>Todos os estilos de linha do projeto (subcategorias de Linhas), por nome.</summary>
        public static List<GraphicsStyle> LineStyles(Document doc)
        {
            var result = new List<GraphicsStyle>();
            try
            {
                Category lines = Category.GetCategory(doc, BuiltInCategory.OST_Lines);
                if (lines == null) return result;
                foreach (Category sub in lines.SubCategories)
                {
                    GraphicsStyle gs = sub.GetGraphicsStyle(GraphicsStyleType.Projection);
                    if (gs != null) result.Add(gs);
                }
            }
            catch
            {
                // Sem estilos de linha acessíveis.
            }
            return result.OrderBy(g => g.Name).ToList();
        }
    }
}
