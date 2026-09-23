using System;
using Autodesk.Revit.DB;
using DetalhaBIM.Core;

namespace DetalhaBIM.Services
{
    /// <summary>Cria elevações orientadas para uma direção qualquer e ajusta recorte/profundidade.</summary>
    public static class ElevationFactory
    {
        /// <summary>
        /// Cria uma elevação a partir de um marcador em <paramref name="point"/>, girando o
        /// marcador até que a vista "olhe" no sentido oposto a <paramref name="towardViewer"/>
        /// (ou seja, <paramref name="towardViewer"/> aponta do objeto para o observador).
        /// </summary>
        public static ViewSection Create(Document doc, ElementId elevationTypeId, ViewPlan hostPlan, XYZ point, XYZ towardViewer, int scale)
        {
            XYZ p = new XYZ(point.X, point.Y, hostPlan.GenLevel?.Elevation ?? point.Z);
            ElevationMarker marker = ElevationMarker.CreateElevationMarker(doc, elevationTypeId, p, Math.Max(1, scale));
            ViewSection view = marker.CreateElevation(doc, hostPlan.Id, 0);
            doc.Regenerate();

            XYZ current = Geo.FlatDir(view.ViewDirection);
            XYZ wanted = Geo.FlatDir(towardViewer);
            double angle = Math.Atan2(current.CrossProduct(wanted).Z, current.DotProduct(wanted));
            if (Math.Abs(angle) > 1e-6)
            {
                ElementTransformUtils.RotateElement(doc, marker.Id, Line.CreateBound(p, p + XYZ.BasisZ), angle);
                doc.Regenerate();
            }
            return view;
        }

        /// <summary>Define a profundidade da vista (plano de corte distante), sem linha de corte.</summary>
        public static void SetFarClip(View view, double depth)
        {
            try
            {
                Q.Set(view, BuiltInParameter.VIEWER_BOUND_FAR_CLIPPING, 2); // 2 = cortar sem linha
                Q.Set(view, BuiltInParameter.VIEWER_BOUND_OFFSET_FAR, depth);
            }
            catch
            {
                // Mantém a profundidade padrão do Revit.
            }
        }

        /// <summary>Oculta o marcador nas plantas (visível apenas em escalas mais detalhadas que 1:1).</summary>
        public static void HideMarkerInPlans(View view)
        {
            try
            {
                Q.Set(view, BuiltInParameter.SECTION_COARSER_SCALE_PULLDOWN_METRIC, 1);
            }
            catch
            {
                // parâmetro indisponível
            }
        }
    }
}
