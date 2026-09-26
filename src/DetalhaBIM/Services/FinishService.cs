using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    /// <summary>
    /// Pintura de acabamentos (ferramenta Pintura do Revit): na face clicada ou em todas as faces de
    /// parede que delimitam um ambiente, com piso e teto opcionais, e preenchimento do quadro de
    /// acabamentos do ambiente.
    /// </summary>
    public class FinishService
    {
        private readonly Document _doc;
        private readonly Report _report;

        public FinishService(Document doc, Report report)
        {
            _doc = doc;
            _report = report;
        }

        /// <summary>Pinta (ou remove a pintura de) uma face clicada. Deve ser chamado dentro de uma transação.</summary>
        public bool PaintFace(Reference r, Material m, bool remove)
        {
            Element e = _doc.GetElement(r);
            Face face = e?.GetGeometryObjectFromReference(r) as Face;
            if (face == null)
            {
                _report.Warn("A face clicada não pôde ser lida.");
                return false;
            }
            if (Paint(e.Id, face, m, remove)) return true;
            _report.Warn(e is FamilyInstance
                ? "Faces de famílias (móveis, marcenaria...) não aceitam a ferramenta Pintura — troque o material no tipo da família."
                : $"A face de {e.Category?.Name ?? "elemento"} não aceitou a pintura.");
            return false;
        }

        private bool Paint(ElementId id, Face face, Material m, bool remove)
        {
            try
            {
                if (remove)
                {
                    if (_doc.IsPainted(id, face)) _doc.RemovePaint(id, face);
                }
                else
                {
                    _doc.Paint(id, face, m.Id);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Pinta as faces de parede (e opcionalmente piso e teto) que delimitam o ambiente.
        /// Retorna quantas faces foram pintadas.
        /// </summary>
        public int PaintRoom(Room room, Material m, bool remove, bool floor, bool ceiling)
        {
            int n = 0, failed = 0;
            var done = new HashSet<string>();
            string Key(ElementId id, Face face) => id + "|" + (face.Reference?.ConvertToStableRepresentation(_doc) ?? face.GetHashCode().ToString());

            // Paredes: face lateral que passa pelo contorno do ambiente.
            foreach (IList<BoundarySegment> loop in RoomGeo.Segments(room))
            foreach (BoundarySegment seg in loop)
            {
                if (!(_doc.GetElement(seg.ElementId) is Wall wall)) continue;
                Curve c = seg.GetCurve();
                if (c == null) continue;
                Face face = SideFaceAt(wall, c.Evaluate(0.5, true));
                if (face == null || !done.Add(Key(wall.Id, face))) continue;
                if (Paint(wall.Id, face, m, remove)) n++;
                else failed++;
            }

            // Pilares e demais elementos delimitadores (laterais), piso e teto: geometria do ambiente.
            try
            {
                var calc = new SpatialElementGeometryCalculator(_doc, RoomGeo.FinishOptions());
                SpatialElementGeometryResults res = calc.CalculateSpatialElementGeometry(room);
                foreach (Face f in res.GetGeometry().Faces)
                foreach (SpatialElementBoundarySubface sub in res.GetBoundaryFaceInfo(f))
                {
                    ElementId host = sub.SpatialBoundaryElement?.HostElementId ?? ElementId.InvalidElementId;
                    if (host == ElementId.InvalidElementId) continue;
                    bool side = sub.SubfaceType == SubfaceType.Side && !(_doc.GetElement(host) is Wall);
                    bool want = side || (floor && sub.SubfaceType == SubfaceType.Bottom) || (ceiling && sub.SubfaceType == SubfaceType.Top);
                    if (!want) continue;
                    Face bf = sub.GetBoundingElementFace();
                    if (bf == null || !done.Add(Key(host, bf))) continue;
                    if (Paint(host, bf, m, remove)) n++;
                    else failed++;
                }
            }
            catch
            {
                if (floor || ceiling) _report.Warn($"Ambiente {RoomGeo.Label(room)}: piso/teto não puderam ser pintados.");
            }
            if (failed > 0) _report.Warn($"Ambiente {RoomGeo.Label(room)}: {failed} face(s) não aceitaram a pintura (ex.: famílias ou vínculos).");
            return n;
        }

        /// <summary>Face lateral da parede que passa pelo ponto do contorno do ambiente (a mais próxima).</summary>
        private static Face SideFaceAt(Wall wall, XYZ p)
        {
            Face best = null;
            double bestD = double.MaxValue;
            foreach (ShellLayerType side in new[] { ShellLayerType.Exterior, ShellLayerType.Interior })
            {
                IList<Reference> refs;
                try
                {
                    refs = HostObjectUtils.GetSideFaces(wall, side);
                }
                catch
                {
                    continue;
                }
                foreach (Reference r in refs)
                {
                    if (!(wall.GetGeometryObjectFromReference(r) is Face f)) continue;
                    double d = Distance(f, p);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = f;
                    }
                }
            }
            return bestD < Conv.Cm(30) ? best : null;
        }

        private static double Distance(Face f, XYZ p)
        {
            double best = double.MaxValue;
            // O ponto do contorno está na altura do nível; testa algumas alturas.
            foreach (double dz in new[] { 0.0, Conv.Cm(50), Conv.M(1.2) })
            {
                try
                {
                    IntersectionResult ir = f.Project(p + XYZ.BasisZ * dz);
                    if (ir != null) best = Math.Min(best, Geo.Flat(ir.XYZPoint - p).GetLength());
                }
                catch
                {
                    // fora da face
                }
            }
            return best;
        }

        /// <summary>Preenche os acabamentos do ambiente (quadro de acabamentos). Campos vazios não mudam.</summary>
        public void SetRoomFinishes(Room room, string floor, string wall, string ceiling, string baseboard)
        {
            int n = 0;
            if (!string.IsNullOrWhiteSpace(floor) && Q.Set(room, BuiltInParameter.ROOM_FINISH_FLOOR, floor.Trim())) n++;
            if (!string.IsNullOrWhiteSpace(wall) && Q.Set(room, BuiltInParameter.ROOM_FINISH_WALL, wall.Trim())) n++;
            if (!string.IsNullOrWhiteSpace(ceiling) && Q.Set(room, BuiltInParameter.ROOM_FINISH_CEILING, ceiling.Trim())) n++;
            if (!string.IsNullOrWhiteSpace(baseboard) && Q.Set(room, BuiltInParameter.ROOM_FINISH_BASE, baseboard.Trim())) n++;
            if (n > 0) _report.Count("ambientes com o quadro de acabamentos preenchido");
        }

        /// <summary>Paleta simples para materiais novos (cor de sombreamento).</summary>
        public static readonly (string name, Color color)[] Colors =
        {
            ("Branco gelo", new Color(242, 240, 234)),
            ("Cinza claro", new Color(200, 200, 198)),
            ("Grafite", new Color(90, 92, 96)),
            ("Bege", new Color(222, 205, 178)),
            ("Madeira", new Color(160, 112, 72)),
            ("Terracota", new Color(186, 98, 64)),
            ("Verde sálvia", new Color(156, 175, 136)),
            ("Azul", new Color(96, 134, 172)),
            ("Preto", new Color(35, 35, 35)),
        };
    }
}
