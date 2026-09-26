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

    /// <summary>Plantas, cortes, elevações e vistas 3D ortogonais (isométricas).</summary>
    public class DimensionViewAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication app, CategorySet selectedCategories)
        {
            Document doc = app.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) return false;
            View v = app.ActiveUIDocument.ActiveView;
            return v != null && !v.IsTemplate && (v is ViewPlan || v is ViewSection || (v is View3D v3 && !v3.IsPerspective));
        }
    }

    /// <summary>Plantas, cortes e elevações.</summary>
    public class PlanOrSectionAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication app, CategorySet selectedCategories)
        {
            Document doc = app.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) return false;
            View v = app.ActiveUIDocument.ActiveView;
            return v != null && !v.IsTemplate && (v is ViewPlan || v is ViewSection);
        }
    }

    /// <summary>Sempre disponível (configurações e ajuda).</summary>
    public class AlwaysAvailable : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication app, CategorySet selectedCategories) => true;
    }
}
