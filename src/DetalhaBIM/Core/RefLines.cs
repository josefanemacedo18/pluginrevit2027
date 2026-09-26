using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    /// <summary>
    /// Linhas invisíveis usadas como referência de cota onde o modelo não oferece uma face
    /// paralela — pontas chanfradas, pontas e extremos de paredes curvas, famílias sem planos de
    /// referência. Em plantas, cortes e elevações são linhas de detalhe; em vistas 3D, linhas de
    /// modelo no plano da cota. Todas usam o estilo &lt;Linhas invisíveis&gt;.
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

        /// <summary>Vistas que aceitam linhas de referência.</summary>
        public static bool Supported(View v) => v is ViewPlan || v is ViewSection || (v is View3D v3 && !v3.IsPerspective);

        public int Created { get; private set; }

        private readonly List<ElementId> _batch = new List<ElementId>();

        /// <summary>
        /// Apaga as linhas criadas desde a última chamada que não são referência de nenhuma das cotas
        /// informadas (ex.: pontos descartados por coincidirem com uma face, ou cotas que falharam).
        /// </summary>
        public void DeleteUnused(IEnumerable<Dimension> kept)
        {
            var used = new HashSet<ElementId>();
            foreach (Dimension d in kept ?? Enumerable.Empty<Dimension>())
            {
                if (d == null || !d.IsValidObject) continue;
                try
                {
                    foreach (Reference r in d.References) used.Add(r.ElementId);
                }
                catch
                {
                    // cota sem referências legíveis
                }
            }
            foreach (ElementId id in _batch)
            {
                if (used.Contains(id)) continue;
                try
                {
                    _doc.Delete(id);
                }
                catch
                {
                    // já removida
                }
            }
            _batch.Clear();
        }

        /// <summary>Esquece as linhas criadas até aqui (elas ficam no desenho).</summary>
        public void Keep() => _batch.Clear();

        /// <summary>
        /// Cria uma linha curta, perpendicular a <paramref name="dir"/>, passando por
        /// <paramref name="p"/>, e a devolve como referência na posição informada. Em vistas 3D,
        /// <paramref name="planeNormal"/> é a normal do plano da cota.
        /// </summary>
        public RefPos Across(XYZ p, XYZ dir, double position, int priority = 6, XYZ planeNormal = null)
        {
            if (!Supported(_view) || p == null || dir == null || dir.IsZeroLength()) return null;
            dir = dir.Normalize();
            bool is3D = _view is View3D;
            XYZ normal = is3D ? (planeNormal ?? _view.ViewDirection) : _view.ViewDirection;
            XYZ n = normal.CrossProduct(dir);
            if (n.IsZeroLength()) return null;
            n = n.Normalize();
            XYZ q = is3D ? p : OnViewPlane(p);
            double half = Conv.Mm(40);
            CurveElement ce = NewCurve(Line.CreateBound(q - n * half, q + n * half), is3D ? n.CrossProduct(dir).Normalize() : null);
            Reference r = ce?.GeometryCurve?.Reference;
            return r == null ? null : new RefPos(r, position, ce, priority);
        }

        /// <summary>Arco invisível (referência para cotas de raio e de comprimento de arco).</summary>
        public CurveElement Arc(Arc arc)
        {
            if (!Supported(_view) || _view is View3D) return null;
            try
            {
                Arc a = (Arc)arc.CreateTransformed(Transform.CreateTranslation(OnViewPlane(arc.Center) - arc.Center));
                return NewCurve(a, null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Linha invisível entre dois pontos (no plano da vista).</summary>
        public CurveElement Segment(XYZ a, XYZ b)
        {
            if (!Supported(_view) || _view is View3D || a.DistanceTo(b) < Conv.Mm(1)) return null;
            return NewCurve(Line.CreateBound(OnViewPlane(a), OnViewPlane(b)), null);
        }

        private CurveElement NewCurve(Curve c, XYZ modelPlaneNormal)
        {
            try
            {
                CurveElement ce;
                if (_view is View3D)
                {
                    Plane plane = Plane.CreateByNormalAndOrigin(modelPlaneNormal, c.GetEndPoint(0));
                    ce = _doc.Create.NewModelCurve(c, SketchPlane.Create(_doc, plane));
                }
                else
                {
                    ce = _doc.Create.NewDetailCurve(_view, c);
                }
                if (ce == null) return null;
                GraphicsStyle style = InvisibleStyle();
                if (style != null)
                {
                    try
                    {
                        ce.LineStyle = style;
                    }
                    catch
                    {
                        // Mantém o estilo padrão se o invisível não for aceito.
                    }
                }
                Created++;
                _batch.Add(ce.Id);
                return ce;
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

        // ================================================================== pontos de curvas

        /// <summary>
        /// Pontos de uma curva que interessam a uma cota ao longo de <paramref name="d"/>: as duas
        /// pontas e, em arcos, os pontos extremos (onde a tangente é perpendicular a d) que ficam
        /// dentro do trecho do arco.
        /// </summary>
        public static List<XYZ> KeyPoints(Curve c, XYZ d)
        {
            var pts = new List<XYZ>();
            if (c == null || !c.IsBound) return pts;
            pts.Add(c.GetEndPoint(0));
            pts.Add(c.GetEndPoint(1));
            XYZ dir = Geo.FlatDir(d);
            if (dir.IsZeroLength() || c is Line) return pts;

            if (c is Arc arc)
            {
                XYZ ctr = arc.Center;
                P2 p0 = new P2(arc.GetEndPoint(0).X - ctr.X, arc.GetEndPoint(0).Y - ctr.Y);
                P2 p1 = new P2(arc.GetEndPoint(1).X - ctr.X, arc.GetEndPoint(1).Y - ctr.Y);
                XYZ m = arc.Evaluate(0.5, true);
                P2 pm = new P2(m.X - ctr.X, m.Y - ctr.Y);
                foreach (double s in new[] { 1.0, -1.0 })
                {
                    P2 q = new P2(dir.X * s, dir.Y * s);
                    if (Plano2D.OnArc(p0, pm, p1, q)) pts.Add(new XYZ(ctr.X + dir.X * s * arc.Radius, ctr.Y + dir.Y * s * arc.Radius, m.Z));
                }
                return pts;
            }

            // Outras curvas (elipses, splines): extremos pela tesselação.
            IList<XYZ> t = c.Tessellate();
            XYZ lo = t.OrderBy(p => p.DotProduct(dir)).First(), hi = t.OrderBy(p => p.DotProduct(dir)).Last();
            pts.Add(lo);
            pts.Add(hi);
            return pts;
        }
    }
}
