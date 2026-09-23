using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    /// <summary>Face plana vertical de um elemento, com sua referência (cotável) e extensões.</summary>
    public class FaceInfo
    {
        public Element Element { get; set; }
        public Reference Reference { get; set; }
        public XYZ Normal { get; set; }
        public XYZ Origin { get; set; }
        /// <summary>Direção horizontal contida no plano da face.</summary>
        public XYZ H { get; set; }
        public double HMin { get; set; }
        public double HMax { get; set; }
        public double ZMin { get; set; }
        public double ZMax { get; set; }

        public double PositionAlong(XYZ dir) => Origin.DotProduct(dir);

        /// <summary>Verifica se a projeção horizontal da face cobre o ponto (dentro da tolerância).</summary>
        public bool CoversH(XYZ p, double tol)
        {
            double v = p.DotProduct(H);
            return v >= HMin - tol && v <= HMax + tol;
        }

        public double DistanceToPlane(XYZ p) => Math.Abs((p - Origin).DotProduct(Normal));
    }

    /// <summary>Referência pronta para cotagem, com sua posição ao longo da direção da cota.</summary>
    public class RefPos
    {
        public RefPos(Reference reference, double position, Element element, int priority = 5)
        {
            Reference = reference;
            Position = position;
            Element = element;
            Priority = priority;
        }

        public Reference Reference { get; }
        public double Position { get; }
        public Element Element { get; }
        /// <summary>Menor valor = preferida quando duas referências coincidem.</summary>
        public int Priority { get; }
    }

    /// <summary>
    /// Extrai faces verticais de elementos (paredes, pilares...) e encontra as que são
    /// atravessadas por uma linha em planta. Resultados ficam em cache durante o comando.
    /// </summary>
    public class FaceFinder
    {
        private readonly View _view;
        private readonly Dictionary<ElementId, List<FaceInfo>> _cache = new Dictionary<ElementId, List<FaceInfo>>();

        public FaceFinder(View view)
        {
            _view = view;
        }

        public List<FaceInfo> VerticalFaces(Element e)
        {
            if (e == null) return new List<FaceInfo>();
            if (_cache.TryGetValue(e.Id, out List<FaceInfo> cached)) return cached;

            var result = new List<FaceInfo>();
            var options = new Options { ComputeReferences = true, IncludeNonVisibleObjects = false };
            if (_view != null && !(_view is ViewSheet)) options.View = _view;

            GeometryElement ge = null;
            try
            {
                ge = e.get_Geometry(options);
            }
            catch
            {
                // Alguns elementos não fornecem geometria na vista.
            }
            if (ge == null)
            {
                options = new Options { ComputeReferences = true, IncludeNonVisibleObjects = false };
                ge = e.get_Geometry(options);
            }

            if (ge != null)
            {
                foreach (Solid solid in Solids(ge))
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (!(face is PlanarFace pf) || pf.Reference == null) continue;
                        XYZ n = pf.FaceNormal;
                        if (Math.Abs(n.Z) > 0.01) continue;
                        FaceInfo info = Build(e, pf);
                        if (info != null) result.Add(info);
                    }
                }
            }

            _cache[e.Id] = result;
            return result;
        }

        public List<FaceInfo> FacesNormalTo(Element e, XYZ dir)
        {
            return VerticalFaces(e).Where(f => Geo.IsParallel(f.Normal, dir)).ToList();
        }

        /// <summary>Faces com normal paralela à linha A→B que são atravessadas por ela.</summary>
        public List<RefPos> Crossing(IEnumerable<Element> elements, XYZ a, XYZ b, double tolH, int priority = 5)
        {
            var result = new List<RefPos>();
            XYZ d = Geo.FlatDir(b - a);
            if (d.IsZeroLength()) return result;
            double len = Geo.Flat(b - a).GetLength();
            double a0 = a.DotProduct(d);

            foreach (Element e in elements)
            {
                foreach (FaceInfo f in FacesNormalTo(e, d))
                {
                    double pos = f.PositionAlong(d);
                    double t = pos - a0;
                    if (t < -tolH || t > len + tolH) continue;
                    XYZ q = a + d * t;
                    if (!f.CoversH(q, tolH)) continue;
                    result.Add(new RefPos(f.Reference, pos, e, priority));
                }
            }
            return result;
        }

        /// <summary>
        /// Face do elemento que coincide com um segmento de contorno (ex.: face de acabamento da
        /// parede que delimita um ambiente).
        /// </summary>
        public FaceInfo FaceAtSegment(Element e, Curve segment, double maxDistance)
        {
            if (!(segment is Line line)) return null;
            XYZ dir = Geo.FlatDir(line.Direction);
            XYZ normal = Geo.LeftNormal(dir);
            XYZ mid = Geo.Mid(line);

            FaceInfo best = null;
            double bestScore = double.MaxValue;
            foreach (FaceInfo f in FacesNormalTo(e, normal))
            {
                double dist = f.DistanceToPlane(Geo.WithZ(mid, f.Origin.Z));
                if (dist > maxDistance) continue;
                double score = dist + (f.CoversH(mid, 0.01) ? 0 : 1000);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = f;
                }
            }
            return best;
        }

        private static FaceInfo Build(Element e, PlanarFace pf)
        {
            XYZ n = Geo.FlatDir(pf.FaceNormal);
            if (n.IsZeroLength()) return null;
            XYZ h = XYZ.BasisZ.CrossProduct(n).Normalize();

            IList<XYZ> vertices;
            try
            {
                vertices = pf.Triangulate().Vertices;
            }
            catch
            {
                return null;
            }
            if (vertices == null || vertices.Count == 0) return null;

            double hMin = double.MaxValue, hMax = double.MinValue, zMin = double.MaxValue, zMax = double.MinValue;
            foreach (XYZ v in vertices)
            {
                double hv = v.DotProduct(h);
                hMin = Math.Min(hMin, hv);
                hMax = Math.Max(hMax, hv);
                zMin = Math.Min(zMin, v.Z);
                zMax = Math.Max(zMax, v.Z);
            }

            return new FaceInfo
            {
                Element = e,
                Reference = pf.Reference,
                Normal = n,
                Origin = pf.Origin,
                H = h,
                HMin = hMin,
                HMax = hMax,
                ZMin = zMin,
                ZMax = zMax,
            };
        }

        private static IEnumerable<Solid> Solids(GeometryElement ge)
        {
            foreach (GeometryObject obj in ge)
            {
                if (obj is Solid s && s.Faces.Size > 0)
                {
                    yield return s;
                }
                else if (obj is GeometryInstance gi)
                {
                    GeometryElement inst = gi.GetInstanceGeometry();
                    if (inst == null) continue;
                    foreach (Solid inner in Solids(inst)) yield return inner;
                }
            }
        }
    }
}
