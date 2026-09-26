using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    /// <summary>Face plana com referência cotável, com posição e normal em coordenadas do projeto.</summary>
    public class ScanFace
    {
        public PlanarFace Face { get; set; }
        public Reference Reference { get; set; }
        public XYZ Normal { get; set; }
        public XYZ Origin { get; set; }
        /// <summary>Transformação do símbolo para o projeto (identidade para paredes, pisos...).</summary>
        public Transform Transform { get; set; }
    }

    /// <summary>Aresta ou linha reta (inclusive linhas simbólicas da família) com referência cotável.</summary>
    public class ScanEdge
    {
        public Reference Reference { get; set; }
        public XYZ Direction { get; set; }
        public XYZ Mid { get; set; }
    }

    /// <summary>
    /// Leitura da geometria de um elemento qualquer (parede, móvel, marcenaria, bloco de
    /// componente...) para cotá-lo.
    /// <para>
    /// Importante: segundo a documentação do Revit, somente as referências da geometria do
    /// <b>símbolo</b> (GetSymbolGeometry sem transformação) servem para cotas; as da geometria da
    /// instância são cópias e geram cotas inválidas. Por isso as referências vêm do símbolo e as
    /// posições são convertidas para o projeto pela transformação da instância.
    /// </para>
    /// </summary>
    public class ElementScan
    {
        private ElementScan(Element e)
        {
            Element = e;
        }

        public Element Element { get; }
        /// <summary>Faces planas com referência válida para cotas.</summary>
        public List<ScanFace> Faces { get; } = new List<ScanFace>();
        /// <summary>Arestas e linhas simbólicas retas com referência válida para cotas.</summary>
        public List<ScanEdge> Edges { get; } = new List<ScanEdge>();
        /// <summary>
        /// Pontos da geometria (para as extensões), em coordenadas do projeto: dos sólidos, quando
        /// houver (linhas simbólicas como o arco de abertura de portas não aumentam a medida); só as
        /// linhas quando a família não tiver sólidos na vista.
        /// </summary>
        public List<XYZ> Points { get; } = new List<XYZ>();

        private readonly List<XYZ> _solidPoints = new List<XYZ>();
        private readonly List<XYZ> _curvePoints = new List<XYZ>();

        public static ElementScan Of(Element e, View view)
        {
            var scan = new ElementScan(e);
            GeometryElement ge = Geometry(e, view) ?? Geometry(e, null);
            if (ge != null) scan.ReadTop(ge);
            scan.Points.AddRange(scan._solidPoints.Count > 0 ? scan._solidPoints : scan._curvePoints);
            if (scan.Points.Count == 0)
            {
                BoundingBoxXYZ bb = e.get_BoundingBox(view) ?? e.get_BoundingBox(null);
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

        private static GeometryElement Geometry(Element e, View view)
        {
            try
            {
                var options = new Options { ComputeReferences = true, IncludeNonVisibleObjects = false };
                if (view != null && !(view is ViewSheet)) options.View = view;
                return e.get_Geometry(options);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Geometria do nível superior do elemento.</summary>
        private void ReadTop(GeometryElement ge)
        {
            foreach (GeometryObject obj in ge)
            {
                if (obj is GeometryInstance gi)
                {
                    // Pontos (extensão) pela geometria da instância, que inclui famílias aninhadas.
                    try
                    {
                        GeometryElement inst = gi.GetInstanceGeometry();
                        if (inst != null) ReadPoints(inst);
                    }
                    catch
                    {
                        // Instância sem geometria.
                    }
                    // Referências pela geometria do símbolo (as únicas válidas para cotas).
                    try
                    {
                        GeometryElement sym = gi.GetSymbolGeometry();
                        if (sym != null) ReadRefs(sym, gi.Transform);
                    }
                    catch
                    {
                        // Símbolo indisponível.
                    }
                }
                else
                {
                    ReadObject(obj, Transform.Identity, true);
                }
            }
        }

        private void ReadRefs(GeometryElement ge, Transform tr)
        {
            foreach (GeometryObject obj in ge)
            {
                // Famílias aninhadas: as referências internas não são confiáveis para cotas.
                if (obj is GeometryInstance) continue;
                ReadObject(obj, tr, false);
            }
        }

        private void ReadObject(GeometryObject obj, Transform tr, bool points)
        {
            switch (obj)
            {
                case Solid s when s.Faces.Size > 0:
                    foreach (Face f in s.Faces)
                    {
                        if (!(f is PlanarFace pf) || pf.Reference == null) continue;
                        Faces.Add(new ScanFace
                        {
                            Face = pf,
                            Reference = pf.Reference,
                            Normal = tr.OfVector(pf.FaceNormal).Normalize(),
                            Origin = tr.OfPoint(pf.Origin),
                            Transform = tr,
                        });
                    }
                    foreach (Edge edge in s.Edges)
                    {
                        Curve c = AsCurve(edge);
                        if (c == null) continue;
                        if (points)
                        {
                            _solidPoints.Add(c.GetEndPoint(0));
                            _solidPoints.Add(c.GetEndPoint(1));
                        }
                        if (c is Line l && edge.Reference != null) AddEdge(edge.Reference, l, tr);
                    }
                    break;
                case Curve curve:
                    if (points && curve.IsBound)
                    {
                        _curvePoints.Add(curve.GetEndPoint(0));
                        _curvePoints.Add(curve.GetEndPoint(1));
                    }
                    if (curve is Line line && line.IsBound && curve.Reference != null) AddEdge(curve.Reference, line, tr);
                    break;
            }
        }

        private void AddEdge(Reference r, Line l, Transform tr)
        {
            Edges.Add(new ScanEdge
            {
                Reference = r,
                Direction = tr.OfVector(l.Direction).Normalize(),
                Mid = tr.OfPoint(Geo.Mid(l)),
            });
        }

        private void ReadPoints(GeometryElement ge)
        {
            foreach (GeometryObject obj in ge)
            {
                switch (obj)
                {
                    case Solid s when s.Faces.Size > 0:
                        foreach (Edge edge in s.Edges)
                        {
                            Curve c = AsCurve(edge);
                            if (c == null) continue;
                            _solidPoints.Add(c.GetEndPoint(0));
                            _solidPoints.Add(c.GetEndPoint(1));
                        }
                        break;
                    case Curve curve when curve.IsBound:
                        _curvePoints.Add(curve.GetEndPoint(0));
                        _curvePoints.Add(curve.GetEndPoint(1));
                        break;
                    case GeometryInstance gi:
                        try
                        {
                            GeometryElement inst = gi.GetInstanceGeometry();
                            if (inst != null) ReadPoints(inst);
                        }
                        catch
                        {
                            // sem geometria
                        }
                        break;
                }
            }
        }

        private static Curve AsCurve(Edge edge)
        {
            try
            {
                return edge.AsCurve();
            }
            catch
            {
                return null;
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
        /// ordem de preferência: planos de referência da família, faces e arestas/linhas.
        /// <paramref name="planeNormal"/> (vistas 3D) restringe as arestas às paralelas ao plano da cota.
        /// </summary>
        public List<IList<RefPos>> ExtremePairs(XYZ axis, XYZ planeNormal = null)
        {
            var result = new List<IList<RefPos>>();
            axis = axis.Normalize();
            Extent(axis, out double min, out double max);

            IList<RefPos> family = FamilyPair(axis, min, max);
            if (family != null) result.Add(family);

            IList<RefPos> faces = FacePair(axis, min, max);
            if (faces != null) result.Add(faces);

            IList<RefPos> edges = EdgePair(axis, planeNormal, min, max);
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
                Transform inv = f.Transform.Inverse;
                XYZ q = inv.OfPoint(Geo.WithZ(p, f.Origin.Z));
                IntersectionResult ir = null;
                try
                {
                    ir = f.Face.Project(q);
                }
                catch
                {
                    // Ponto fora da face.
                }
                if (ir == null || ir.XYZPoint == null || Geo.Flat(f.Transform.OfPoint(ir.XYZPoint) - Geo.WithZ(p, f.Origin.Z)).GetLength() > Conv.Mm(1)) continue;
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

        /// <summary>
        /// As referências só valem se estiverem nas extremidades reais do objeto (ex.: a casca externa
        /// de um armário pode vir de uma família aninhada, cujas faces não são cotáveis; as faces
        /// internas dariam uma medida errada).
        /// </summary>
        private static bool AtExtents(double pa, double pb, double min, double max) =>
            Math.Abs(pa - min) <= Conv.Mm(5) && Math.Abs(pb - max) <= Conv.Mm(5);

        /// <summary>Faces mais externas perpendiculares ao eixo (só se estiverem nas extremidades do objeto).</summary>
        private IList<RefPos> FacePair(XYZ axis, double min, double max)
        {
            List<ScanFace> parallel = Faces.Where(f => Geo.IsParallel(f.Normal, axis)).ToList();
            if (parallel.Count < 2) return null;
            ScanFace a = parallel.OrderBy(f => f.Origin.DotProduct(axis)).First();
            ScanFace b = parallel.OrderBy(f => f.Origin.DotProduct(axis)).Last();
            double pa = a.Origin.DotProduct(axis), pb = b.Origin.DotProduct(axis);
            if (pb - pa < Conv.Mm(1) || !AtExtents(pa, pb, min, max)) return null;
            return new List<RefPos> { new RefPos(a.Reference, pa, Element, 2), new RefPos(b.Reference, pb, Element, 2) };
        }

        private IList<RefPos> EdgePair(XYZ axis, XYZ planeNormal, double min, double max)
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
            if (pb - pa < Conv.Mm(1) || !AtExtents(pa, pb, min, max)) return null;
            return new List<RefPos> { new RefPos(a.Reference, pa, Element, 3), new RefPos(b.Reference, pb, Element, 3) };
        }
    }
}
