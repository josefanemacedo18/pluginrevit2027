using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class FinishOptions
    {
        public Material Wall { get; set; }
        public Material Floor { get; set; }
        public Material Ceiling { get; set; }
        public bool RemoveWall { get; set; }
        public bool RemoveFloor { get; set; }
        public bool RemoveCeiling { get; set; }

        public bool SetParameters { get; set; }
        public bool KeepFilled { get; set; } = true;
        public string TextFloor { get; set; }
        public string TextWall { get; set; }
        public string TextCeiling { get; set; }
        public string TextBase { get; set; }

        public bool Paints => Wall != null || Floor != null || Ceiling != null || RemoveWall || RemoveFloor || RemoveCeiling;
    }

    /// <summary>
    /// Acabamentos por ambiente: pinta (ferramenta Pintura) as faces de paredes, piso e teto que
    /// delimitam cada ambiente e preenche os parâmetros de acabamento usados nos quadros.
    /// </summary>
    public class FinishService
    {
        private readonly Document _doc;
        private readonly Report _report;
        private readonly SpatialElementGeometryCalculator _calc;

        public FinishService(Document doc, Report report)
        {
            _doc = doc;
            _report = report;
            _calc = new SpatialElementGeometryCalculator(doc, RoomGeo.FinishOptions());
        }

        public void Apply(Room room, FinishOptions o)
        {
            if (o.Paints) Paint(room, o);
            if (o.SetParameters)
            {
                int n = 0;
                n += SetText(room, BuiltInParameter.ROOM_FINISH_FLOOR, o.TextFloor, o.KeepFilled);
                n += SetText(room, BuiltInParameter.ROOM_FINISH_WALL, o.TextWall, o.KeepFilled);
                n += SetText(room, BuiltInParameter.ROOM_FINISH_CEILING, o.TextCeiling, o.KeepFilled);
                n += SetText(room, BuiltInParameter.ROOM_FINISH_BASE, o.TextBase, o.KeepFilled);
                if (n > 0) _report.Count("ambientes com acabamentos preenchidos");
            }
        }

        private static int SetText(Room room, BuiltInParameter bip, string text, bool keep)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            if (keep && !string.IsNullOrWhiteSpace(Q.Text(room, bip))) return 0;
            return Q.Set(room, bip, text.Trim()) ? 1 : 0;
        }

        private void Paint(Room room, FinishOptions o)
        {
            SpatialElementGeometryResults res;
            try
            {
                res = _calc.CalculateSpatialElementGeometry(room);
            }
            catch (Exception ex)
            {
                _report.Warn($"Ambiente {RoomGeo.Label(room)}: não foi possível calcular a geometria ({ex.Message}).");
                return;
            }

            int painted = 0, failed = 0;
            var done = new HashSet<string>();
            foreach (Face face in res.GetGeometry().Faces)
            {
                IList<SpatialElementBoundarySubface> subs;
                try
                {
                    subs = res.GetBoundaryFaceInfo(face);
                }
                catch
                {
                    continue;
                }
                foreach (SpatialElementBoundarySubface sub in subs)
                {
                    ElementId hostId = sub.SpatialBoundaryElement?.HostElementId ?? ElementId.InvalidElementId;
                    if (hostId == ElementId.InvalidElementId) continue; // elemento de vínculo
                    Element host = _doc.GetElement(hostId);
                    Face bf = sub.GetBoundingElementFace();
                    if (host == null || bf == null) continue;

                    Material m;
                    bool remove;
                    switch (sub.SubfaceType)
                    {
                        case SubfaceType.Side:
                            m = o.Wall;
                            remove = o.RemoveWall;
                            break;
                        case SubfaceType.Bottom:
                            m = o.Floor;
                            remove = o.RemoveFloor;
                            break;
                        default:
                            m = o.Ceiling;
                            remove = o.RemoveCeiling;
                            break;
                    }
                    if (m == null && !remove) continue;
                    string key = hostId + "|" + (bf.Reference?.ConvertToStableRepresentation(_doc) ?? bf.GetHashCode().ToString());
                    if (!done.Add(key)) continue;
                    try
                    {
                        if (remove)
                        {
                            if (_doc.IsPainted(hostId, bf)) _doc.RemovePaint(hostId, bf);
                        }
                        else
                        {
                            _doc.Paint(hostId, bf, m.Id);
                        }
                        painted++;
                    }
                    catch
                    {
                        failed++;
                    }
                }
            }
            _report.Count("faces pintadas/atualizadas", painted);
            if (painted > 0) _report.Count("ambientes com faces pintadas");
            if (failed > 0) _report.Warn($"Ambiente {RoomGeo.Label(room)}: {failed} face(s) não aceitaram pintura (ex.: elementos de família ou vínculos).");
        }
    }
}
