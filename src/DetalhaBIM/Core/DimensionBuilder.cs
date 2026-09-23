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
            if (_view is ViewPlan || _view == null) return p;
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
