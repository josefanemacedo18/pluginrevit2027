using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    public class TileOptions
    {
        /// <summary>Dimensões da peça e da junta (pés).</summary>
        public double Width { get; set; }
        public double Length { get; set; }
        public double Joint { get; set; }
        /// <summary>0 = junta centralizada, 1 = peça centralizada, 2 = canto, 3 = ponto clicado.</summary>
        public int Start { get; set; }
        public XYZ Point { get; set; }
        /// <summary>Ângulo em relação à maior parede do ambiente (graus).</summary>
        public double AngleDeg { get; set; }
        public bool DrawLines { get; set; } = true;
        public GraphicsStyle Style { get; set; }
        public bool Group { get; set; } = true;
        public bool Note { get; set; } = true;
        public bool Replace { get; set; } = true;
        public string Description { get; set; }
        /// <summary>Perda (fração: 0,10 = 10%).</summary>
        public double Loss { get; set; }
        /// <summary>m² por caixa (0 = não calcular).</summary>
        public double BoxM2 { get; set; }
    }

    /// <summary>
    /// Paginação de piso: desenha as juntas da peça escolhida dentro do contorno de acabamento do
    /// ambiente (contornando pilares) e conta peças inteiras e cortadas, com área de compra e caixas.
    /// </summary>
    public class TileLayoutService
    {
        public const string GroupPrefix = "Paginação - ";
        public const int MaxLines = 4000;

        private readonly Document _doc;
        private readonly ViewPlan _view;
        private readonly Report _report;

        public TileLayoutService(Document doc, ViewPlan view, Report report)
        {
            _doc = doc;
            _view = view;
            _report = report;
        }

        public static readonly string[] Headers =
        {
            "Ambiente", "Área (m²)", "Peça (cm)", "Junta (mm)", "Inteiras", "Cortadas", "Total de peças",
            "Área + perda (m²)", "Caixas",
        };

        /// <summary>Cria a paginação de um ambiente e devolve a linha do quantitativo.</summary>
        public string[] Layout(Room room, TileOptions o)
        {
            PlanFrame frame = RoomGeo.Frame(room);
            List<List<P2>> loops = Loops(room, frame);
            if (loops.Count == 0)
            {
                _report.Warn($"Ambiente {RoomGeo.Label(room)}: contorno inválido.");
                return null;
            }

            P2 x = P2.FromAngle(o.AngleDeg * Math.PI / 180);
            P2 start = StartPoint(loops, frame, o);
            int mode = o.Start >= 2 ? 2 : o.Start;
            P2 origin = Plano2D.GridOrigin(mode, start, x, o.Width, o.Length, o.Joint);
            TileCount count = Plano2D.CountTiles(loops, origin, x, o.Width, o.Length, o.Joint);

            if (o.Replace) RemovePrevious(room);
            var ids = new List<ElementId>();
            if (o.DrawLines)
            {
                List<(P2 a, P2 b)> lines = Plano2D.JointLines(loops, origin, x, o.Width, o.Length, o.Joint);
                if (lines.Count > MaxLines)
                {
                    _report.Warn($"Ambiente {RoomGeo.Label(room)}: {lines.Count} juntas — peça pequena demais para desenhar linha a linha (limite {MaxLines}). O quantitativo foi calculado; use um padrão de preenchimento para o desenho.");
                }
                else
                {
                    double z = _view.GenLevel?.Elevation ?? 0;
                    foreach ((P2 a, P2 b) in lines)
                    {
                        if ((b - a).Length < Conv.Mm(2)) continue;
                        try
                        {
                            DetailCurve dc = _doc.Create.NewDetailCurve(_view, Line.CreateBound(frame.World(a.X, a.Y, z), frame.World(b.X, b.Y, z)));
                            if (o.Style != null) dc.LineStyle = o.Style;
                            ids.Add(dc.Id);
                        }
                        catch
                        {
                            // Trecho recusado (muito curto): ignora.
                        }
                    }
                    _report.Count("linhas de junta", ids.Count);
                }
            }

            string text = NoteText(count, o);
            if (o.Note)
            {
                try
                {
                    XYZ p = RoomGeo.Point(room);
                    XYZ pos = Geo.WithZ(p, _view.GenLevel?.Elevation ?? p.Z) - frame.Y * Conv.PaperMm(9, _view);
                    ElementId typeId = _doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                    var opts = new TextNoteOptions(typeId) { HorizontalAlignment = HorizontalTextAlignment.Center, Rotation = frame.Angle };
                    TextNote note = TextNote.Create(_doc, _view.Id, pos, text, opts);
                    ids.Add(note.Id);
                }
                catch
                {
                    _report.Warn($"Ambiente {RoomGeo.Label(room)}: nota de quantidade não pôde ser criada.");
                }
            }

            if (o.Group && ids.Count > 1)
            {
                try
                {
                    Group g = _doc.Create.NewGroup(ids);
                    SetGroupName(g, GroupPrefix + RoomGeo.Label(room));
                }
                catch
                {
                    // Sem grupo: as linhas continuam na vista.
                }
            }
            _report.Count("ambientes paginados");
            return Row(room, count, o);
        }

        private static string NoteText(TileCount c, TileOptions o)
        {
            string piece = $"{Conv.Format(Conv.ToCm(o.Width), 1)}x{Conv.Format(Conv.ToCm(o.Length), 1)}";
            string head = string.IsNullOrWhiteSpace(o.Description) ? piece : $"{o.Description.Trim()} {piece}";
            return $"{head} - {c.Total} pç ({c.Inteiras} inteiras + {c.Cortadas} cortadas)";
        }

        private static string[] Row(Room room, TileCount c, TileOptions o)
        {
            double area = Conv.ToM2(c.AreaRegiao);
            double buy = area * (1 + o.Loss);
            string boxes = o.BoxM2 > 0 ? ((int)Math.Ceiling(buy / o.BoxM2 - 1e-9)).ToString() : "-";
            return new[]
            {
                RoomGeo.Label(room),
                Conv.Format(area, 2),
                $"{Conv.Format(Conv.ToCm(o.Width), 1)} x {Conv.Format(Conv.ToCm(o.Length), 1)}",
                Conv.Format(Conv.ToCm(o.Joint) * 10, 1),
                c.Inteiras.ToString(),
                c.Cortadas.ToString(),
                c.Total.ToString(),
                Conv.Format(buy, 2),
                boxes,
            };
        }

        /// <summary>Laços do contorno de acabamento no sistema local do ambiente (plano 2D).</summary>
        private static List<List<P2>> Loops(Room room, PlanFrame frame)
        {
            var result = new List<List<P2>>();
            foreach (IList<BoundarySegment> loop in RoomGeo.Segments(room))
            {
                var pts = new List<P2>();
                foreach (BoundarySegment s in loop)
                {
                    IList<XYZ> t = s.GetCurve().Tessellate();
                    for (int i = 0; i < t.Count - 1; i++) pts.Add(new P2(frame.LX(t[i]), frame.LY(t[i])));
                }
                if (pts.Count >= 3) result.Add(pts);
            }
            return result;
        }

        private static P2 StartPoint(List<List<P2>> loops, PlanFrame frame, TileOptions o)
        {
            double minX = loops.SelectMany(l => l).Min(p => p.X), maxX = loops.SelectMany(l => l).Max(p => p.X);
            double minY = loops.SelectMany(l => l).Min(p => p.Y), maxY = loops.SelectMany(l => l).Max(p => p.Y);
            switch (o.Start)
            {
                case 2:
                    return new P2(minX, minY);
                case 3 when o.Point != null:
                    return new P2(frame.LX(o.Point), frame.LY(o.Point));
                default:
                    return new P2((minX + maxX) / 2, (minY + maxY) / 2);
            }
        }

        private void RemovePrevious(Room room)
        {
            string name = GroupPrefix + RoomGeo.Label(room);
            List<Group> old = new FilteredElementCollector(_doc, _view.Id).OfClass(typeof(Group)).Cast<Group>()
                .Where(g => g.GroupType != null && (g.GroupType.Name == name || g.GroupType.Name.StartsWith(name + " (", StringComparison.Ordinal)))
                .ToList();
            foreach (Group g in old)
            {
                try
                {
                    ElementId typeId = g.GetTypeId();
                    List<ElementId> members = g.UngroupMembers().ToList();
                    _doc.Delete(members);
                    if (_doc.GetElement(typeId) is GroupType gt && gt.Groups.IsEmpty) _doc.Delete(typeId);
                    _report.Count("paginações anteriores substituídas");
                }
                catch
                {
                    // Mantém a anterior se não puder ser removida.
                }
            }
        }

        private void SetGroupName(Group g, string name)
        {
            string clean = ViewTools.Sanitize(name);
            var used = new HashSet<string>(new FilteredElementCollector(_doc).OfClass(typeof(GroupType)).Cast<GroupType>()
                .Where(t => t.Id != g.GroupType.Id).Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
            string n = clean;
            int i = 2;
            while (used.Contains(n)) n = $"{clean} ({i++})";
            g.GroupType.Name = n;
        }
    }
}
