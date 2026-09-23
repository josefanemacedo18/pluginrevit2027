using System;
using System.Globalization;
using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    /// <summary>
    /// Conversões entre as unidades internas do Revit (pés decimais) e as unidades usadas
    /// no dia a dia do arquiteto (cm, m, mm e mm "no papel").
    /// </summary>
    public static class Conv
    {
        public static double Cm(double value) => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Centimeters);
        public static double M(double value) => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Meters);
        public static double Mm(double value) => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);

        public static double ToCm(double feet) => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Centimeters);
        public static double ToM(double feet) => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Meters);
        public static double ToM2(double squareFeet) => UnitUtils.ConvertFromInternalUnits(squareFeet, UnitTypeId.SquareMeters);

        /// <summary>
        /// Converte uma distância em milímetros medida na folha impressa para a distância
        /// equivalente no modelo, de acordo com a escala da vista (ex.: 8 mm em 1:50 = 40 cm).
        /// </summary>
        public static double PaperMm(double mm, View view)
        {
            int scale = view != null && view.Scale > 0 ? view.Scale : 50;
            return Mm(mm) * scale;
        }

        /// <summary>Lê número aceitando vírgula ou ponto como separador decimal.</summary>
        public static bool TryParse(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim().Replace(" ", string.Empty);
            if (t.Contains(",") && t.Contains(".")) t = t.Replace(".", string.Empty);
            t = t.Replace(',', '.');
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static string Format(double value, int decimals = 2)
        {
            return Math.Round(value, decimals).ToString("F" + decimals, CultureInfo.GetCultureInfo("pt-BR"));
        }
    }
}
