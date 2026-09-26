using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    /// <summary>
    /// Cria cadeias de cotas a partir de referências posicionadas. Remove referências
    /// coincidentes e, se o Revit rejeitar alguma referência, refaz a cadeia descartando
    /// apenas as referências inválidas.
    /// </summary>
    public class DimensionBuilder
    {
        private readonly Document _doc;
        private readonly View _view;
        private readonly DimensionType _type;
        private readonly double _minSegment;

        public DimensionBuilder(Document doc, View view, DimensionType type, double minSegment)
        {
            _doc = doc;
            _view = view;
            _type = type;
            _minSegment = Math.Max(minSegment, Conv.Mm(2));
        }

        /// <summary>
        /// Somente em vistas 3D: normal do plano de trabalho onde a próxima cota será desenhada.
        /// Deve ser perpendicular à direção da cota; se não for informada, usa o plano mais
        /// voltado para o observador.
        /// </summary>
        public XYZ PlaneNormal { get; set; }

        public View View => _view;

        public static DimensionBuilder FromSettings(Document doc, View view, CotasSettings s, string typeName = null)
        {
            DimensionType type = Q.DimensionType(doc, typeName ?? s.TipoCota);
            return new DimensionBuilder(doc, view, type, Conv.Cm(s.MenorSegmentoCm));
        }

        /// <summary>Ordena por posição e mantém uma referência por posição (a de menor prioridade).</summary>
        public List<RefPos> Dedupe(IEnumerable<RefPos> refs)
        {
            var result = new List<RefPos>();
            foreach (RefPos r in refs.Where(r => r?.Reference != null).OrderBy(r => r.Position))
            {
                if (result.Count > 0 && r.Position - result[result.Count - 1].Position < _minSegment)
                {
                    RefPos last = result[result.Count - 1];
                    if (r.Priority < last.Priority) result[result.Count - 1] = r;
                    continue;
                }
                result.Add(r);
            }
            return result;
        }

        /// <summary>Somente as duas referências extremas (cota total).</summary>
        public List<RefPos> Extremes(IEnumerable<RefPos> refs)
        {
            List<RefPos> list = Dedupe(refs);
            if (list.Count < 2) return list;
            return new List<RefPos> { list.First(), list.Last() };
        }

        /// <summary>
        /// Cria a cadeia de cotas ao longo de <paramref name="dir"/> (horizontal em plantas, podendo
        /// ser vertical em elevações), com a linha de cota passando por <paramref name="through"/>.
        /// As posições das referências devem ser medidas ao longo da mesma direção.
        /// Retorna null se não houver ao menos duas referências válidas.
        /// </summary>
        public Dimension Create(IEnumerable<RefPos> refs, XYZ dir, XYZ through)
        {
            List<RefPos> list = Dedupe(refs);
            if (list.Count < 2) return null;
            if (dir == null || dir.IsZeroLength()) return null;
            XYZ d = dir.Normalize();
            through = OnViewPlane(through);

            Dimension dim = TryCreate(list, d, through);
            if (dim != null) return dim;

            // Plano B: acrescenta uma referência por vez e mantém apenas as aceitas pelo Revit.
            var accepted = new List<RefPos>();
            foreach (RefPos candidate in list)
            {
                if (accepted.Count == 0)
                {
                    accepted.Add(candidate);
                    continue;
                }
                var test = new List<RefPos>(accepted) { candidate };
                if (Probe(test, d, through))
                {
                    accepted.Add(candidate);
                }
                else if (accepted.Count == 1 && Probe(new List<RefPos> { candidate, list.Last() }, d, through))
                {
                    // A primeira referência era a inválida.
                    accepted[0] = candidate;
                }
            }
            return accepted.Count >= 2 ? TryCreate(accepted, d, through) : null;
        }

        /// <summary>
        /// Em elevações/cortes, leva o ponto da linha de cota para o plano da vista. Em plantas,
        /// o ponto é mantido (a coordenada Z é definida pelo chamador).
        /// </summary>
        private XYZ OnViewPlane(XYZ p)
        {
            if (_view is ViewPlan || _view is View3D || _view == null) return p;
            try
            {
                XYZ n = _view.ViewDirection.Normalize();
                return p - n * (p - _view.Origin).DotProduct(n);
            }
            catch
            {
                return p;
            }
        }

        /// <summary>
        /// Tenta cada conjunto de referências na ordem (ex.: planos de referência da família, faces,
        /// arestas e, por último, linhas de referência) e retorna a primeira cota aceita pelo Revit
        /// <b>e que meça o esperado</b>: a cota criada é conferida com a distância entre as posições
        /// das referências; se não bater (referência inválida ou medindo outra coisa), é apagada e a
        /// próxima alternativa é tentada.
        /// </summary>
        public Dimension CreateFirst(IEnumerable<IList<RefPos>> candidates, XYZ dir, XYZ through, double tolerance = -1)
        {
            double tol = tolerance > 0 ? tolerance : Conv.Mm(3);
            XYZ d = dir.Normalize();
            XYZ p = OnViewPlane(through);
            var tried = new List<List<RefPos>>();
            foreach (IList<RefPos> refs in candidates)
            {
                if (refs == null || refs.Count(r => r?.Reference != null) < 2) continue;
                List<RefPos> list = Dedupe(refs);
                if (list.Count < 2) continue;
                tried.Add(list);
                double expected = list.Max(r => r.Position) - list.Min(r => r.Position);
                Dimension dim = TryCreate(list, d, p);
                if (dim == null) continue;
                if (Matches(dim, expected, tol)) return dim;
                Delete(dim);
            }

            // Plano B (como nas demais cadeias): se o Revit recusou alguma referência, refaz a cadeia só
            // com as aceitas — começando pela última alternativa, a mais segura (linhas auxiliares).
            for (int i = tried.Count - 1; i >= 0; i--)
            {
                if (tried[i].Count < 3) continue; // pares já foram tentados inteiros
                Dimension dim = Create(tried[i], d, p);
                if (dim != null) return dim;
            }
            return null;
        }

        private void Delete(Dimension d)
        {
            try
            {
                _doc.Delete(d.Id);
            }
            catch
            {
                // Cota já removida pelo Revit.
            }
        }

        /// <summary>
        /// Confere se a cota mede o total esperado (soma dos segmentos). Lê o valor calculado na criação;
        /// só regenera o documento se o valor ainda não estiver disponível.
        /// </summary>
        private bool Matches(Dimension d, double expected, double tol)
        {
            double? total = Total(d);
            if (total == null)
            {
                try
                {
                    _doc.Regenerate();
                }
                catch
                {
                    return false;
                }
                total = Total(d);
            }
            return total != null && total.Value > Conv.Mm(1) && Math.Abs(total.Value - expected) <= tol;
        }

        private static double? Total(Dimension d)
        {
            try
            {
                if (!d.IsValidObject) return null;
                if (d.NumberOfSegments == 0) return d.Value;
                double total = 0;
                foreach (DimensionSegment seg in d.Segments)
                {
                    double? v = seg.Value;
                    if (v == null) return null;
                    total += v.Value;
                }
                return total;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Em vistas 3D o Revit desenha a cota no plano de trabalho da vista: cria um plano que
        /// contém a linha de cota (perpendicular às referências).
        /// </summary>
        private void SetWorkPlane(XYZ origin, XYZ d)
        {
            XYZ n = PlaneNormal;
            if (n == null || n.IsZeroLength() || !Geo.IsPerpendicular(n, d, 0.05))
            {
                XYZ v = _view.ViewDirection;
                n = v - d * v.DotProduct(d);
                if (n.IsZeroLength()) n = d.CrossProduct(XYZ.BasisZ);
                if (n.IsZeroLength()) n = d.CrossProduct(XYZ.BasisX);
            }
            n = (n - d * n.DotProduct(d)).Normalize();
            _view.SketchPlane = SketchPlane.Create(_doc, Plane.CreateByNormalAndOrigin(n, origin));
        }

        private bool Probe(List<RefPos> refs, XYZ d, XYZ through)
        {
            bool ok = false;
            using (var st = new SubTransaction(_doc))
            {
                st.Start();
                try
                {
                    ok = TryCreate(refs, d, through) != null;
                }
                finally
                {
                    st.RollBack();
                }
            }
            return ok;
        }

        private Dimension TryCreate(List<RefPos> refs, XYZ d, XYZ through)
        {
            try
            {
                var array = new ReferenceArray();
                foreach (RefPos r in refs) array.Append(r.Reference);

                double min = refs.Min(r => r.Position);
                double max = refs.Max(r => r.Position);
                double t0 = through.DotProduct(d);
                XYZ p0 = through + d * (min - t0);
                XYZ p1 = through + d * (max - t0);
                if (p0.DistanceTo(p1) < Conv.Mm(1)) return null;
                Line line = Line.CreateBound(p0, p1);
                if (_view is View3D) SetWorkPlane(p0, d);

                return _type != null
                    ? _doc.Create.NewDimension(_view, line, array, _type)
                    : _doc.Create.NewDimension(_view, line, array);
            }
            catch
            {
                return null;
            }
        }
    }
}
