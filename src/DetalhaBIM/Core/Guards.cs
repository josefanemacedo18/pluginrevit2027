using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    public static class Guards
    {
        /// <summary>Exige uma planta ativa (piso/forro/estrutural) e a retorna.</summary>
        public static ViewPlan RequirePlan(CommandContext ctx)
        {
            if (ctx.ActiveView is ViewPlan vp && !vp.IsTemplate && vp.GenLevel != null) return vp;
            throw new UserMessageException("Esta ferramenta funciona em plantas. Abra uma planta de piso ou de forro e tente novamente.");
        }

        /// <summary>Garante um plano de trabalho na vista para permitir cliques de pontos.</summary>
        public static void EnsureWorkPlane(Document doc, View view)
        {
            if (view.SketchPlane != null) return;
            Tx.Run(doc, "Plano de trabalho", () =>
            {
                Plane plane = Plane.CreateByNormalAndOrigin(view.ViewDirection, view.GenLevel != null
                    ? new XYZ(0, 0, view.GenLevel.Elevation)
                    : view.Origin);
                view.SketchPlane = SketchPlane.Create(doc, plane);
            });
        }
    }
}
