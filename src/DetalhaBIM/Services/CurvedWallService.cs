using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    /// <summary>
    /// Cotas de paredes curvas em planta, na face do lado clicado: raio, comprimento do arco e
    /// corda (distância reta entre as pontas).
    /// </summary>
    public class CurvedWallService
    {
        private readonly Document _doc;
        private readonly ViewPlan _view;
        private readonly DimensionBuilder _builder;
        private readonly Report _report;
        private readonly RefLines _lines;
        private readonly double _z;

        public CurvedWallService(Document doc, ViewPlan view, DimensionBuilder builder, Report report)
        {
            _doc = doc;
            _view = view;
            _builder = builder;
            _report = report;
            _lines = new RefLines(doc, view);
            _z = view.GenLevel?.Elevation ?? 0;
        }

        public static bool IsCurved(Wall w) => w?.Location is LocationCurve lc && lc.Curve is Arc;

        /// <summary>Cria as cotas da parede curva. Deve ser chamado dentro de uma transação.</summary>
        public int Dimension(Wall wall, XYZ sidePoint, double offset, bool radius = true, bool arcLength = true, bool chord = true)
        {
            if (!(wall.Location is LocationCurve lc) || !(lc.Curve is Arc cl))
            {
                _report.Warn("A parede escolhida não é curva.");
                return 0;
            }
            Arc face = FaceArc(wall, cl, sidePoint);
            if (face == null)
            {
                _report.Warn($"Parede curva {wall.Id}: face não encontrada.");
                return 0;
            }
            XYZ c = Geo.WithZ(face.Center, _z);
            XYZ p0 = Geo.WithZ(face.GetEndPoint(0), _z), p1 = Geo.WithZ(face.GetEndPoint(1), _z);
            XYZ pm = Geo.WithZ(face.Evaluate(0.5, true), _z);
            // Lado das cotas: para fora da face clicada (mesmo lado do clique).
            bool outward = Geo.Flat(sidePoint - c).GetLength() >= face.Radius;
            int created = 0;
            var dims = new List<Dimension>();
            _lines.Keep();

            CurveElement refArc = (radius || arcLength) ? _lines.Arc(face) : null;
            Reference arcRef = refArc?.GeometryCurve?.Reference;

            if (radius && arcRef != null)
            {
                try
                {
                    RadialDimension rd = RadialDimension.Create(_doc, _view, arcRef, false);
                    if (rd != null)
                    {
                        dims.Add(rd);
                        created++;
                        _report.Count("cotas de raio");
                    }
                }
                catch
                {
                    _report.Warn($"Parede curva {wall.Id}: o Revit não aceitou a cota de raio.");
                }
            }

            if (arcLength && arcRef != null)
            {
                try
                {
                    double r = face.Radius + (outward ? offset : -Math.Min(offset, face.Radius * 0.5));
                    Arc annotation = Arc.Create(c + (p0 - c).Normalize() * r, c + (p1 - c).Normalize() * r, c + (pm - c).Normalize() * r);
                    CurveElement e0 = Radial(c, p0, face.Radius, r);
                    CurveElement e1 = Radial(c, p1, face.Radius, r);
                    if (e0 != null && e1 != null)
                    {
                        var refs = new List<Reference> { e0.GeometryCurve.Reference, e1.GeometryCurve.Reference };
                        ArcLengthDimension ad = ArcLengthDimension.Create(_doc, _view, annotation, arcRef, refs);
                        if (ad != null)
                        {
                            dims.Add(ad);
                            created++;
                            _report.Count("cotas de comprimento de arco");
                        }
                    }
                }
                catch
                {
                    _report.Warn($"Parede curva {wall.Id}: o Revit não aceitou a cota de comprimento do arco.");
                }
            }

            if (chord && p0.DistanceTo(p1) > Conv.Cm(5))
            {
                XYZ t = Geo.FlatDir(p1 - p0);
                XYZ n = Geo.LeftNormal(t);
                // A corda fica do lado de fora do arco (convexo), afastada.
                XYZ mid = (p0 + p1) / 2;
                if ((pm - mid).DotProduct(n) < 0) n = n.Negate();
                double sagitta = Math.Abs((pm - mid).DotProduct(n));
                XYZ through = mid + n * (sagitta + offset * (arcLength ? 2 : 1));
                RefPos a = _lines.Across(p0, t, p0.DotProduct(t), 1);
                RefPos b = _lines.Across(p1, t, p1.DotProduct(t), 1);
                Dimension cd = a != null && b != null ? _builder.Create(new[] { a, b }, t, through) : null;
                if (cd != null)
                {
                    dims.Add(cd);
                    created++;
                    _report.Count("cotas de corda");
                }
            }
            _lines.DeleteUnused(dims);
            return created;
        }

        /// <summary>Linha radial invisível cruzando a ponta do arco (referência da cota de arco).</summary>
        private CurveElement Radial(XYZ c, XYZ p, double r0, double r1)
        {
            XYZ u = (p - c).Normalize();
            double a = Math.Min(r0, r1) - Conv.Cm(5), b = Math.Max(r0, r1) + Conv.Cm(5);
            return _lines.Segment(c + u * Math.Max(Conv.Mm(1), a), c + u * b);
        }

        /// <summary>
        /// Arco da face da parede do lado clicado: raio da face cilíndrica mais próxima do clique
        /// (lida da geometria; se não houver, eixo ± meia espessura).
        /// </summary>
        public static Arc FaceArc(Wall wall, Arc centreline, XYZ sidePoint)
        {
            XYZ c = centreline.Center;
            double dist = Geo.Flat(sidePoint - c).GetLength();
            var radii = new List<double>();
            try
            {
                foreach (ShellLayerType side in new[] { ShellLayerType.Exterior, ShellLayerType.Interior })
                foreach (Reference r in HostObjectUtils.GetSideFaces(wall, side))
                {
                    if (wall.GetGeometryObjectFromReference(r) is CylindricalFace cf) radii.Add(cf.get_Radius(0).GetLength());
                }
            }
            catch
            {
                // Sem faces laterais: usa a espessura.
            }
            if (radii.Count < 2)
            {
                radii.Clear();
                radii.Add(centreline.Radius - wall.Width / 2);
                radii.Add(centreline.Radius + wall.Width / 2);
            }
            double rFace = dist >= radii.Average() ? radii.Max() : radii.Min();
            if (rFace < Conv.Mm(1)) return null;
            try
            {
                XYZ p0 = centreline.GetEndPoint(0), p1 = centreline.GetEndPoint(1), pm = centreline.Evaluate(0.5, true);
                return Arc.Create(c + Geo.Flat(p0 - c).Normalize() * rFace + XYZ.BasisZ * (p0.Z - c.Z),
                    c + Geo.Flat(p1 - c).Normalize() * rFace + XYZ.BasisZ * (p1.Z - c.Z),
                    c + Geo.Flat(pm - c).Normalize() * rFace + XYZ.BasisZ * (pm.Z - c.Z));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Referências (linhas invisíveis) nas pontas e nos pontos extremos de uma curva, ao longo de
        /// <paramref name="d"/>, para completar cadeias de cotas que chegam em paredes curvas.
        /// </summary>
        public static List<RefPos> KeyRefs(RefLines lines, Curve curve, XYZ d, double z, int priority = 6)
        {
            var result = new List<RefPos>();
            if (lines == null || curve == null) return result;
            foreach (XYZ p in RefLines.KeyPoints(curve, d))
            {
                XYZ q = Geo.WithZ(p, z);
                double pos = q.DotProduct(d);
                if (result.Any(r => Math.Abs(r.Position - pos) < Conv.Mm(2))) continue;
                RefPos r = lines.Across(q, d, pos, priority);
                if (r != null) result.Add(r);
            }
            return result;
        }
    }
}
