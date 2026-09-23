using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DetalhaBIM.Ribbon
{
    /// <summary>Disponível quando há um projeto (não família) aberto.</summary>
    public class ProjectAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication app, CategorySet selectedCategories)
        {
            Document doc = app.ActiveUIDocument?.Document;
            return doc != null && !doc.IsFamilyDocument;
        }
    }

    /// <summary>Disponível somente com uma planta (piso, forro ou estrutural) ativa.</summary>
    public class PlanViewAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication app, CategorySet selectedCategories)
        {
            Document doc = app.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) return false;
            return app.ActiveUIDocument.ActiveView is ViewPlan vp && !vp.IsTemplate;
        }
    }

    /// <summary>Sempre disponível (configurações e ajuda).</summary>
    public class AlwaysAvailable : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication app, CategorySet selectedCategories) => true;
    }
}
