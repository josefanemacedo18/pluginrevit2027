using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    /// <summary>Face plana com referência cotável, em coordenadas do projeto.</summary>
    public class ScanFace
    {
        public PlanarFace Face { get; set; }
        public Reference Reference { get; set; }
        public XYZ Normal { get; set; }
        public XYZ Origin { get; set; }
    }

    /// <summary>Aresta reta com referência cotável, em coordenadas do projeto.</summary>
    public class ScanEdge
    {
        public Reference Reference { get; set; }
        public XYZ Direction { get; set; }
        public XYZ Mid { get; set; }
    }

    /// <summary>
    /// Leitura da geometria de um elemento qualquer (parede, móvel, marcenaria, luminária...)
    /// para cotá-lo: faces, arestas e pontos, com referências válidas na vista informada.
    /// Oferece, para cada eixo, várias alternativas de referências extremas (planos de
    /// referência da família, faces da instância, faces do símbolo e arestas), já que cada
    /// família é modelada de um jeito.
    /// </summary>
    public class ElementScan
    {
        private ElementScan(Element e)
        {
            Element = e;
        }

        public Element Element { get; }
        public List<ScanFace> Faces { get; } = new List<ScanFace>();
        public List<ScanFace> SymbolFaces { get; } = new List<ScanFace>();
        public List<ScanEdge> Edges { get; } = new List<ScanEdge>();
        public List<XYZ> Points { get; } = new List<XYZ>();

        public static ElementScan Of(Element e, View view)
        {
            var scan = new ElementScan(e);
            GeometryElement ge = null;
            try
            {
                var options = new Options { ComputeReferences = true, IncludeNonVisibleObjects = false };
                if (view != null && !(view is ViewSheet)) options.View = view;
                ge = e.get_Geometry(options);
            }
            catch
            {
                // Sem geometria na vista: tenta sem vista.
            }
            if (ge == null)
            {
                try
                {
                    ge = e.get_Geometry(new Options { ComputeReferences = true, IncludeNonVisibleObjects = false });
                }
                catch
                {
                    // Elemento sem geometria.
                }
            }
            if (ge != null)
            {
                scan.Read(ge, false, Transform.Identity);
                scan.ReadSymbols(ge, Transform.Identity);
            }
            if (scan.Points.Count == 0)
            {
                BoundingBoxXYZ bb = e.get_BoundingBox(null);
                if (bb != null)
                {
                    Transform t = bb.Transform ?? Transform.Identity;
                    foreach (double x in new[] { bb.Min.X, bb.Max.X })
                    foreach (double y in new[] { bb.Min.Y, bb.Max.Y })
                    foreach (double z in new[] { bb.Min.Z, bb.Max.Z })
                        scan.Points.Add(t.OfPoint(new XYZ(x, y, z)));
                }
            }
            return scan;
        }

        private void Read(GeometryElement ge, bool symbol, Transform tr)
        {
            foreach (GeometryObject obj in ge)
            {
                if (obj is Solid s && s.Faces.Size > 0)
                {
                    foreach (Face f in s.Faces)
                    {
                        if (!(f is PlanarFace pf) || pf.Reference == null) continue;
                        var sf = new ScanFace
                        {
                            Face = pf,
                            Reference = pf.Reference,
                            Normal = tr.OfVector(pf.FaceNormal).Normalize(),
                            Origin = tr.OfPoint(pf.Origin),
                        };
                        (symbol ? SymbolFaces : Faces).Add(sf);
                    }
                    if (symbol) continue;
                    foreach (Edge edge in s.Edges)
                    {
                        Curve c;
                        try
                        {
                            c = edge.AsCurve();
                        }
                        catch
                        {
                            continue;
                        }
                        if (c == null) continue;
                        Points.Add(c.GetEndPoint(0));
                        Points.Add(c.GetEndPoint(1));
                        if (c is Line l && edge.Reference != null)
                            Edges.Add(new ScanEdge { Reference = edge.Reference, Direction = l.Direction.Normalize(), Mid = Geo.Mid(l) });
                    }
                }
                else if (obj is GeometryInstance gi && !symbol)
                {
                    GeometryElement inst = null;
                    try
                    {
                        inst = gi.GetInstanceGeometry();
                    }
                    catch
                    {
                        // Instância sem geometria.
                    }
                    if (inst != null) Read(inst, false, tr);
                }
                else if (obj is GeometryInstance nested && symbol)
                {
                    GeometryElement sym = nested.GetSymbolGeometry();
                    if (sym != null) Read(sym, true, tr.Multiply(nested.Transform));
                }
            }
        }

        private void ReadSymbols(GeometryElement ge, Transform tr)
        {
            foreach (GeometryObject obj in ge)
            {
                if (!(obj is GeometryInstance gi)) continue;
                try
                {
                    GeometryElement sym = gi.GetSymbolGeometry();
                    if (sym != null) Read(sym, true, tr.Multiply(gi.Transform));
                }
                catch
                {
                    // Símbolo indisponível.
                }
            }
        }

        // ================================================================== extensões

        public bool Extent(XYZ axis, out double min, out double max)
        {
            min = double.MaxValue;
            max = double.MinValue;
            foreach (XYZ p in Points)
            {
                double v = p.DotProduct(axis);
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
            return min <= max;
        }

        /// <summary>Centro da caixa envolvente no sistema dos eixos informados.</summary>
        public XYZ Center(XYZ x, XYZ y, XYZ z)
        {
            Extent(x, out double x0, out double x1);
            Extent(y, out double y0, out double y1);
            Extent(z, out double z0, out double z1);
            return x * ((x0 + x1) / 2) + y * ((y0 + y1) / 2) + z * ((z0 + z1) / 2);
        }

        // ================================================================== referências

        /// <summary>
        /// Alternativas de par de referências nas extremidades do elemento ao longo do eixo, na
        /// ordem de preferência. <paramref name="planeNormal"/> (vistas 3D) restringe as arestas
        /// às que ficam paralelas ao plano da cota.
        /// </summary>
        public List<IList<RefPos>> ExtremePairs(XYZ axis, XYZ planeNormal = null)
        {
            var result = new List<IList<RefPos>>();
            axis = axis.Normalize();
            Extent(axis, out double min, out double max);

            IList<RefPos> family = FamilyPair(axis, min, max);
            if (family != null) result.Add(family);

            IList<RefPos> inst = FacePair(Faces, axis);
            if (inst != null) result.Add(inst);

            IList<RefPos> sym = FacePair(SymbolFaces, axis);
            if (sym != null) result.Add(sym);

            IList<RefPos> edges = EdgePair(axis, planeNormal);
            if (edges != null) result.Add(edges);
            return result;
        }

        /// <summary>Referência de centro (eixo) do elemento na direção informada, quando a família a define.</summary>
        public RefPos CenterRef(XYZ axis)
        {
            if (!(Element is FamilyInstance fi)) return null;
            Extent(axis, out double min, out double max);
            FamilyInstanceReferenceType? type = Which(fi, axis, FamilyInstanceReferenceType.CenterLeftRight,
                FamilyInstanceReferenceType.CenterFrontBack, FamilyInstanceReferenceType.CenterElevation);
            if (type == null) return null;
            Reference r = fi.GetReferences(type.Value).FirstOrDefault();
            return r == null ? null : new RefPos(r, (min + max) / 2, fi, 1);
        }

        /// <summary>Todas as faces perpendiculares ao eixo (ex.: extremidades e ombreiras de uma parede).</summary>
        public List<RefPos> FacesAlong(XYZ axis, int priority = 3)
        {
            return Faces.Where(f => Geo.IsParallel(f.Normal, axis))
                .Select(f => new RefPos(f.Reference, f.Origin.DotProduct(axis), Element, priority)).ToList();
        }

        /// <summary>Face voltada para cima mais alta que contém o ponto em planta (ex.: piso acabado).</summary>
        public ScanFace TopFaceAt(XYZ p, double maxZ)
        {
            ScanFace best = null;
            foreach (ScanFace f in Faces)
            {
                if (f.Normal.Z < 0.99 || f.Origin.Z > maxZ) continue;
                XYZ q = Geo.WithZ(p, f.Origin.Z);
                IntersectionResult ir = null;
                try
                {
                    ir = f.Face.Project(q);
                }
                catch
                {
                    // Ponto fora da face.
                }
                if (ir == null || ir.XYZPoint == null || Geo.Flat(ir.XYZPoint - q).GetLength() > Conv.Mm(1)) continue;
                if (best == null || f.Origin.Z > best.Origin.Z) best = f;
            }
            return best;
        }

        private IList<RefPos> FamilyPair(XYZ axis, double min, double max)
        {
            if (!(Element is FamilyInstance fi)) return null;
            Transform t;
            try
            {
                t = fi.GetTransform();
            }
            catch
            {
                return null;
            }

            FamilyInstanceReferenceType lo, hi;
            double sign;
            if (Geo.IsParallel(axis, t.BasisX, 0.999))
            {
                lo = FamilyInstanceReferenceType.Left;
                hi = FamilyInstanceReferenceType.Right;
                sign = axis.DotProduct(t.BasisX);
            }
            else if (Geo.IsParallel(axis, t.BasisY, 0.999))
            {
                lo = FamilyInstanceReferenceType.Front;
                hi = FamilyInstanceReferenceType.Back;
                sign = axis.DotProduct(t.BasisY);
            }
            else if (Geo.IsParallel(axis, t.BasisZ, 0.999))
            {
                lo = FamilyInstanceReferenceType.Bottom;
                hi = FamilyInstanceReferenceType.Top;
                sign = axis.DotProduct(t.BasisZ);
            }
            else
            {
                return null;
            }

            Reference a = fi.GetReferences(lo).FirstOrDefault();
            Reference b = fi.GetReferences(hi).FirstOrDefault();
            if (a == null || b == null) return null;
            return sign > 0
                ? new List<RefPos> { new RefPos(a, min, fi, 1), new RefPos(b, max, fi, 1) }
                : new List<RefPos> { new RefPos(a, max, fi, 1), new RefPos(b, min, fi, 1) };
        }

        private static FamilyInstanceReferenceType? Which(FamilyInstance fi, XYZ axis, FamilyInstanceReferenceType x, FamilyInstanceReferenceType y, FamilyInstanceReferenceType z)
        {
            Transform t = fi.GetTransform();
            if (Geo.IsParallel(axis, t.BasisX, 0.999)) return x;
            if (Geo.IsParallel(axis, t.BasisY, 0.999)) return y;
            if (Geo.IsParallel(axis, t.BasisZ, 0.999)) return z;
            return null;
        }

        private IList<RefPos> FacePair(List<ScanFace> faces, XYZ axis)
        {
            List<ScanFace> parallel = faces.Where(f => Geo.IsParallel(f.Normal, axis)).ToList();
            if (parallel.Count < 2) return null;
            ScanFace a = parallel.OrderBy(f => f.Origin.DotProduct(axis)).First();
            ScanFace b = parallel.OrderBy(f => f.Origin.DotProduct(axis)).Last();
            double pa = a.Origin.DotProduct(axis), pb = b.Origin.DotProduct(axis);
            if (pb - pa < Conv.Mm(1)) return null;
            return new List<RefPos> { new RefPos(a.Reference, pa, Element, 2), new RefPos(b.Reference, pb, Element, 2) };
        }

        private IList<RefPos> EdgePair(XYZ axis, XYZ planeNormal)
        {
            IEnumerable<ScanEdge> edges = Edges.Where(e => Geo.IsPerpendicular(e.Direction, axis, 0.01));
            if (planeNormal != null && !planeNormal.IsZeroLength())
            {
                XYZ inPlane = planeNormal.CrossProduct(axis);
                edges = edges.Where(e => Geo.IsParallel(e.Direction, inPlane) || Geo.IsParallel(e.Direction, planeNormal));
            }
            List<ScanEdge> list = edges.ToList();
            if (list.Count < 2) return null;
            ScanEdge a = list.OrderBy(e => e.Mid.DotProduct(axis)).First();
            ScanEdge b = list.OrderBy(e => e.Mid.DotProduct(axis)).Last();
            double pa = a.Mid.DotProduct(axis), pb = b.Mid.DotProduct(axis);
            if (pb - pa < Conv.Mm(1)) return null;
            return new List<RefPos> { new RefPos(a.Reference, pa, Element, 3), new RefPos(b.Reference, pb, Element, 3) };
        }
    }
}
