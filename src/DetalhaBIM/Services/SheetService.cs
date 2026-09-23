using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    /// <summary>
    /// Cria pranchas e distribui vistas automaticamente dentro da área útil do carimbo
    /// (em linhas, da esquerda para a direita, de cima para baixo), abrindo novas pranchas
    /// quando o espaço acaba.
    /// </summary>
    public class SheetService
    {
        private readonly Document _doc;
        private readonly Report _report;
        private readonly PranchasSettings _s;
        private HashSet<string> _numbers;

        public SheetService(Document doc, Report report, PranchasSettings settings)
        {
            _doc = doc;
            _report = report;
            _s = settings;
        }

        public static List<FamilySymbol> TitleBlocks(Document doc) => Q.Symbols(doc, BuiltInCategory.OST_TitleBlocks);

        public FamilySymbol TitleBlock(string label)
        {
            List<FamilySymbol> all = TitleBlocks(_doc);
            return Q.TypeByLabel(all, label) ?? all.FirstOrDefault();
        }

        public string NextNumber(string prefix, int digits)
        {
            if (_numbers == null)
            {
                _numbers = new HashSet<string>(Q.All<ViewSheet>(_doc).Select(s => s.SheetNumber), StringComparer.OrdinalIgnoreCase);
            }
            for (int i = 1; ; i++)
            {
                string n = prefix + i.ToString().PadLeft(Math.Max(1, digits), '0');
                if (_numbers.Add(n)) return n;
            }
        }

        public ViewSheet CreateSheet(FamilySymbol titleBlock, string name, string number = null)
        {
            if (titleBlock != null && !titleBlock.IsActive) titleBlock.Activate();
            ViewSheet sheet = ViewSheet.Create(_doc, titleBlock?.Id ?? ElementId.InvalidElementId);
            try
            {
                sheet.SheetNumber = number ?? NextNumber(_s.PrefixoNumero, _s.Digitos);
            }
            catch
            {
                // mantém número automático
            }
            try
            {
                sheet.Name = string.IsNullOrWhiteSpace(name) ? "DETALHAMENTO" : name;
            }
            catch
            {
                // nome inválido
            }
            return sheet;
        }

        /// <summary>Distribui as vistas em pranchas (quantas forem necessárias).</summary>
        public List<ViewSheet> Layout(IList<View> views, FamilySymbol titleBlock, string sheetName)
        {
            var sheets = new List<ViewSheet>();
            ViewSheet sheet = null;
            Area area = null;
            double x = 0, y = 0, rowH = 0;
            int onSheet = 0;
            double gap = Conv.Mm(_s.EspacoEntreVistasMm);

            void NewSheet()
            {
                string name = sheets.Count == 0 ? sheetName : $"{sheetName} ({sheets.Count + 1})";
                sheet = CreateSheet(titleBlock, name);
                sheets.Add(sheet);
                _doc.Regenerate();
                area = UsableArea(sheet);
                x = area.Left;
                y = area.Top;
                rowH = 0;
                onSheet = 0;
            }

            foreach (View view in views)
            {
                if (view == null) continue;
                if (sheet == null) NewSheet();

                if (!(view is ViewSchedule) && !Viewport.CanAddViewToSheet(_doc, sheet.Id, view.Id))
                {
                    _report.Warn($"A vista \"{view.Name}\" já está em outra prancha ou não pode ser inserida.");
                    continue;
                }

                (double w, double h) = PlaceTemp(sheet, view, area, out Element placed);
                if (placed == null) continue;

                if (x + w > area.Right && onSheet > 0)
                {
                    x = area.Left;
                    y -= rowH + gap;
                    rowH = 0;
                }
                if (y - h < area.Bottom && onSheet > 0)
                {
                    _doc.Delete(placed.Id);
                    NewSheet();
                    (w, h) = PlaceTemp(sheet, view, area, out placed);
                    if (placed == null) continue;
                }

                Move(placed, new XYZ(x + w / 2, y - h / 2, 0), w, h);
                x += w + gap;
                rowH = Math.Max(rowH, h);
                onSheet++;
            }
            return sheets;
        }

        private (double w, double h) PlaceTemp(ViewSheet sheet, View view, Area area, out Element placed)
        {
            placed = null;
            XYZ center = new XYZ((area.Left + area.Right) / 2, (area.Top + area.Bottom) / 2, 0);
            try
            {
                if (view is ViewSchedule)
                {
                    placed = ScheduleSheetInstance.Create(_doc, sheet.Id, view.Id, center);
                    _doc.Regenerate();
                    BoundingBoxXYZ bb = placed.get_BoundingBox(sheet);
                    return bb == null ? (Conv.Mm(100), Conv.Mm(60)) : (bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y);
                }

                Viewport vp = Viewport.Create(_doc, sheet.Id, view.Id, center);
                placed = vp;
                _doc.Regenerate();
                Outline o = vp.GetBoxOutline();
                return (o.MaximumPoint.X - o.MinimumPoint.X, o.MaximumPoint.Y - o.MinimumPoint.Y);
            }
            catch (Exception ex)
            {
                _report.Warn($"Não foi possível inserir \"{view.Name}\" na prancha: {ex.Message}");
                return (0, 0);
            }
        }

        private void Move(Element placed, XYZ center, double w, double h)
        {
            if (placed is Viewport vp)
            {
                vp.SetBoxCenter(center);
            }
            else if (placed is ScheduleSheetInstance ssi)
            {
                // O ponto de inserção da tabela é o canto superior esquerdo.
                ssi.Point = new XYZ(center.X - w / 2, center.Y + h / 2, 0);
            }
        }

        private class Area
        {
            public double Left, Right, Top, Bottom;
        }

        /// <summary>Área útil da prancha: limites do carimbo menos margens e faixa do carimbo.</summary>
        private Area UsableArea(ViewSheet sheet)
        {
            double minX = 0, minY = 0, maxX = Conv.Mm(841), maxY = Conv.Mm(594); // A1 como padrão
            Element tb = new FilteredElementCollector(_doc, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks).FirstElement();
            BoundingBoxXYZ bb = tb?.get_BoundingBox(sheet);
            if (bb != null && bb.Max.X - bb.Min.X > Conv.Mm(50))
            {
                minX = bb.Min.X;
                minY = bb.Min.Y;
                maxX = bb.Max.X;
                maxY = bb.Max.Y;
            }
            double m = Conv.Mm(_s.MargemMm);
            var area = new Area
            {
                Left = minX + m,
                Right = maxX - m - Conv.Mm(_s.FaixaCarimboDireitaMm),
                Top = maxY - m,
                Bottom = minY + m + Conv.Mm(_s.FaixaCarimboInferiorMm),
            };
            if (area.Right - area.Left < Conv.Mm(100)) area.Right = maxX - m;
            if (area.Top - area.Bottom < Conv.Mm(80)) area.Bottom = minY + m;
            return area;
        }
    }
}
