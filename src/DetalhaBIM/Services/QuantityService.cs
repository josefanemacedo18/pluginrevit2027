using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    /// <summary>Quantidades de acabamento de um ambiente (unidades do Revit: pés e pés²).</summary>
    public class RoomQuantities
    {
        public Room Room { get; set; }
        public double FloorArea { get; set; }
        public double Perimeter { get; set; }
        public double WallLength { get; set; }
        public double Height { get; set; }
        public bool HeightFromCeiling { get; set; }
        public double WallGross { get; set; }
        public double Openings { get; set; }
        public int OpeningCount { get; set; }
        public double DoorWidths { get; set; }
        public double WallNet => Math.Max(0, WallGross - Openings);
        public double Baseboard => Math.Max(0, WallLength - DoorWidths);
        public double CeilingArea { get; set; }
    }

    /// <summary>
    /// Levantamento de acabamentos por ambiente: piso, paredes (descontando portas e janelas),
    /// teto e rodapé (descontando as portas), para orçamento e compra de materiais.
    /// </summary>
    public class QuantityService
    {
        private readonly Document _doc;
        private List<Ceiling> _ceilings;

        public QuantityService(Document doc)
        {
            _doc = doc;
        }

        public static readonly string[] Headers =
        {
            "Nº", "Ambiente", "Nível", "Piso (m²)", "Perímetro (m)", "Pé-direito (m)", "Paredes bruta (m²)",
            "Vãos (m²)", "Paredes líquida (m²)", "Rodapé (m)", "Teto (m²)", "Piso + perda (m²)", "Paredes + perda (m²)",
            "Acab. piso", "Acab. paredes", "Acab. teto", "Rodapé (acab.)",
        };

        public RoomQuantities Measure(Room room, bool useCeiling)
        {
            var q = new RoomQuantities { Room = room, FloorArea = room.Area, CeilingArea = room.Area };
            double baseZ = RoomGeo.BaseElevation(room);
            q.Height = RoomGeo.TopElevation(room) - baseZ;
            if (useCeiling)
            {
                double? c = CeilingHeight(room, baseZ);
                if (c.HasValue)
                {
                    q.Height = c.Value;
                    q.HeightFromCeiling = true;
                }
            }

            var seen = new HashSet<ElementId>();
            foreach (IList<BoundarySegment> loop in RoomGeo.Segments(room))
            {
                foreach (BoundarySegment seg in loop)
                {
                    Curve c = seg.GetCurve();
                    if (c == null) continue;
                    q.Perimeter += c.Length;
                    Element e = _doc.GetElement(seg.ElementId);
                    bool linked = e == null && seg.LinkElementId != ElementId.InvalidElementId;
                    if (e is CurveElement || (e == null && !linked)) continue; // linha separadora: sem parede
                    q.WallLength += c.Length;
                    if (!(e is Wall wall) || !(c is Line line)) continue;

                    XYZ t = Geo.FlatDir(line.Direction);
                    double lo = Math.Min(line.GetEndPoint(0).DotProduct(t), line.GetEndPoint(1).DotProduct(t));
                    double hi = Math.Max(line.GetEndPoint(0).DotProduct(t), line.GetEndPoint(1).DotProduct(t));
                    foreach (FamilyInstance fi in Q.OpeningsIn(_doc, new[] { wall.Id }))
                    {
                        if (seen.Contains(fi.Id)) continue;
                        XYZ p = Geo.ElementPoint(fi);
                        if (p == null) continue;
                        double pos = p.DotProduct(t);
                        if (pos < lo || pos > hi) continue;
                        if (Geo.Flat(p - line.Project(p).XYZPoint).GetLength() > wall.Width + Conv.Cm(5)) continue;
                        seen.Add(fi.Id);
                        double w = Q.OpeningWidth(fi) ?? 0, h = Q.OpeningHeight(fi) ?? 0;
                        double sill = Q.Double(fi, BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM) ?? 0;
                        // Só a parte do vão abaixo do teto considerado.
                        double visible = Math.Max(0, Math.Min(h, q.Height - sill));
                        q.Openings += w * visible;
                        q.OpeningCount++;
                        if (fi.Category?.BuiltInCategory == BuiltInCategory.OST_Doors || sill < Conv.Cm(5)) q.DoorWidths += w;
                    }
                }
            }
            q.WallGross = q.WallLength * q.Height;
            return q;
        }

        /// <summary>Altura do forro mais baixo sobre o ambiente, se houver.</summary>
        private double? CeilingHeight(Room room, double baseZ)
        {
            XYZ p = RoomGeo.Point(room);
            if (p == null) return null;
            _ceilings ??= Q.All<Ceiling>(_doc);
            double top = RoomGeo.TopElevation(room) + Conv.Cm(50);
            double? best = null;
            foreach (Ceiling c in _ceilings)
            {
                BoundingBoxXYZ bb = c.get_BoundingBox(null);
                if (bb == null || p.X < bb.Min.X || p.X > bb.Max.X || p.Y < bb.Min.Y || p.Y > bb.Max.Y) continue;
                if (bb.Min.Z <= baseZ + Conv.Cm(50) || bb.Min.Z > top) continue;
                double h = bb.Min.Z - baseZ;
                if (best == null || h < best) best = h;
            }
            return best;
        }

        public static string[] Row(RoomQuantities q, double lossFloor, double lossWall)
        {
            Room r = q.Room;
            return new[]
            {
                RoomGeo.Number(r),
                RoomGeo.Name(r),
                r.Level?.Name ?? string.Empty,
                Conv.Format(Conv.ToM2(q.FloorArea), 2),
                Conv.Format(Conv.ToM(q.Perimeter), 2),
                Conv.Format(Conv.ToM(q.Height), 2) + (q.HeightFromCeiling ? " (forro)" : string.Empty),
                Conv.Format(Conv.ToM2(q.WallGross), 2),
                Conv.Format(Conv.ToM2(q.Openings), 2),
                Conv.Format(Conv.ToM2(q.WallNet), 2),
                Conv.Format(Conv.ToM(q.Baseboard), 2),
                Conv.Format(Conv.ToM2(q.CeilingArea), 2),
                Conv.Format(Conv.ToM2(q.FloorArea) * (1 + lossFloor), 2),
                Conv.Format(Conv.ToM2(q.WallNet) * (1 + lossWall), 2),
                Q.Text(r, BuiltInParameter.ROOM_FINISH_FLOOR),
                Q.Text(r, BuiltInParameter.ROOM_FINISH_WALL),
                Q.Text(r, BuiltInParameter.ROOM_FINISH_CEILING),
                Q.Text(r, BuiltInParameter.ROOM_FINISH_BASE),
            };
        }

        /// <summary>Linha de totais (somente colunas numéricas somáveis).</summary>
        public static string[] Total(IList<string[]> rows)
        {
            var total = new string[Headers.Length];
            total[1] = "TOTAL";
            foreach (int i in new[] { 3, 4, 6, 7, 8, 9, 10, 11, 12 })
                total[i] = Conv.Format(rows.Sum(r => Conv.TryParse(r[i], out double v) ? v : 0), 2);
            for (int i = 0; i < total.Length; i++) total[i] ??= string.Empty;
            return total;
        }
    }
}
