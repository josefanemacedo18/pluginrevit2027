using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    /// <summary>Etiquetagem automática de ambientes, portas, janelas e outras categorias.</summary>
    public class TagService
    {
        private readonly Document _doc;
        private readonly Report _report;

        public TagService(Document doc, Report report)
        {
            _doc = doc;
            _report = report;
        }

        public static bool SupportsRoomTags(View v) => v is ViewPlan || v is ViewSection;

        public int TagRooms(View view, FamilySymbol tagType, bool skipTagged)
        {
            if (!SupportsRoomTags(view)) return 0;
            var tagged = new HashSet<ElementId>(
                new FilteredElementCollector(_doc, view.Id).OfCategory(BuiltInCategory.OST_RoomTags)
                    .OfType<RoomTag>().Select(t => t.TaggedLocalRoomId));

            int count = 0;
            foreach (Room room in new FilteredElementCollector(_doc, view.Id).OfCategory(BuiltInCategory.OST_Rooms).OfType<Room>())
            {
                if (!RoomGeo.IsPlaced(room) || (skipTagged && tagged.Contains(room.Id))) continue;
                XYZ p = RoomGeo.Point(room);
                try
                {
                    RoomTag tag = _doc.Create.NewRoomTag(new LinkElementId(room.Id), new UV(p.X, p.Y), view.Id);
                    if (tag == null) continue;
                    if (tagType != null) tag.ChangeTypeId(tagType.Id);
                    count++;
                }
                catch
                {
                    _report.Warn($"Não foi possível etiquetar o ambiente {RoomGeo.Label(room)} na vista {view.Name}.");
                }
            }
            return count;
        }

        /// <summary>
        /// Etiqueta elementos de uma categoria. Portas recebem a etiqueta do lado interno e
        /// janelas do lado externo, afastadas da parede para não sobrepor o desenho.
        /// </summary>
        public int TagCategory(View view, BuiltInCategory category, FamilySymbol tagType, bool skipTagged, double offsetPaperMm)
        {
            var tagged = new HashSet<ElementId>();
            foreach (IndependentTag t in new FilteredElementCollector(_doc, view.Id).OfClass(typeof(IndependentTag)).Cast<IndependentTag>())
            {
                foreach (ElementId id in t.GetTaggedLocalElementIds()) tagged.Add(id);
            }

            int count = 0;
            bool warned = false;
            foreach (Element e in Q.InView(_doc, view, category))
            {
                if (skipTagged && tagged.Contains(e.Id)) continue;
                XYZ p = TagPoint(e, view, category, offsetPaperMm);
                if (p == null) continue;
                try
                {
                    IndependentTag tag = tagType != null
                        ? IndependentTag.Create(_doc, tagType.Id, view.Id, new Reference(e), false, TagOrientation.Horizontal, p)
                        : IndependentTag.Create(_doc, view.Id, new Reference(e), false, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, p);
                    if (tag != null) count++;
                }
                catch
                {
                    if (!warned)
                    {
                        _report.Warn($"Não foi possível etiquetar {Category(category)} na vista {view.Name}. Verifique se há uma família de etiqueta carregada para a categoria.");
                        warned = true;
                    }
                }
            }
            return count;
        }

        private static XYZ TagPoint(Element e, View view, BuiltInCategory category, double offsetPaperMm)
        {
            XYZ p = Geo.ElementPoint(e, view);
            if (p == null) return null;
            if (!(view is ViewPlan) || !(e is FamilyInstance fi) || !(fi.Host is Wall wall)) return p;

            XYZ facing = Geo.FlatDir(fi.FacingOrientation);
            if (facing.IsZeroLength()) return p;
            double distance = wall.Width / 2 + Conv.PaperMm(offsetPaperMm, view);
            double side = category == BuiltInCategory.OST_Windows ? 1 : -1;
            return p + facing * (distance * side);
        }

        public static string Category(BuiltInCategory c)
        {
            switch (c)
            {
                case BuiltInCategory.OST_Doors: return "portas";
                case BuiltInCategory.OST_Windows: return "janelas";
                case BuiltInCategory.OST_Rooms: return "ambientes";
                case BuiltInCategory.OST_Furniture: return "mobiliário";
                case BuiltInCategory.OST_PlumbingFixtures: return "peças sanitárias";
                case BuiltInCategory.OST_LightingFixtures: return "luminárias";
                default: return "elementos";
            }
        }
    }
}
