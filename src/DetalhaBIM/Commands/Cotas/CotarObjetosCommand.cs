using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Cotas
{
    /// <summary>
    /// Cota largura, profundidade e altura de paredes e de qualquer família (móveis,
    /// marcenaria, louças, equipamentos) em vistas 3D isométricas, plantas, cortes e elevações.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CotarObjetosCommand : CommandBase
    {
        protected override string Title => "Cotar Objetos (3D e 2D)";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            View view = ctx.ActiveView;
            if (!ObjectDimensionService.Supports(view))
                throw new UserMessageException("Abra uma vista 3D isométrica (não perspectiva), uma planta, um corte ou uma elevação e tente novamente.");
            CotasSettings s = ctx.Settings.Cotas;
            bool is3D = view is View3D;

            var dlg = new OptionsDialog("cotar-objetos", Title,
                "Selecione paredes, móveis, marcenaria ou qualquer família: o DetalhaBIM cota as dimensões de cada um na vista ativa — inclusive em vistas 3D isométricas.",
                Theme.Cotas, ctx.MainWindow, "Selecionar");
            FormBuilder f = dlg.Form;
            f.Section("Dimensões");
            f.Check("largura", "Largura (paredes: comprimento)", true);
            f.Check("profundidade", "Profundidade (paredes: espessura) — plantas e 3D", true);
            f.Check("altura", "Altura — cortes, elevações e 3D", true);
            f.Check("vaos", "Paredes: cadeia com os vãos de portas e janelas", true);
            f.Section("Posição");
            f.Number("afastamento", "Distância do objeto até a cota", s.PrimeiraLinhaMm, "mm na folha");
            f.Radio("lado", "Lado (plantas, cortes e elevações)", new[] { "Abaixo e à esquerda", "Acima e à direita" }, 0);
            f.Combo("tipo", "Tipo de cota", ProjectChoices.DimensionTypes(doc), Choices.Or(s.TipoCota, Choices.Default));
            if (is3D)
                f.Hint("Vistas 3D: para receber cotas a vista precisa estar travada. O DetalhaBIM salva a orientação e trava a vista (ou uma cópia dela, se for a vista {3D} padrão). Nas vistas 3D as cotas ficam do lado voltado para você.");
            if (!dlg.Run()) return Result.Cancelled;

            var filter = new PredicateFilter(e => e.Category != null && e.Category.CategoryType == CategoryType.Model
                                                  && !(e is SpatialElement) && !(e is ElementType)
                                                  && e.Category.BuiltInCategory != BuiltInCategory.OST_Rooms);
            List<Element> elements = Pick.SelectedOrPick(ctx.UiDoc, filter, "Selecione os objetos a cotar e clique em Concluir");
            if (elements.Count == 0) return Result.Cancelled;

            if (view is View3D v3 && !v3.IsLocked)
            {
                v3 = Locked3D(ctx, v3);
                view = v3;
            }

            var options = new ObjectDimensionOptions
            {
                Width = f.Bool("largura"),
                Depth = f.Bool("profundidade"),
                Height = f.Bool("altura"),
                WallOpenings = f.Bool("vaos"),
                Offset = Conv.PaperMm(f.Double("afastamento"), view),
                AboveRight = f.Index("lado") == 1,
            };
            var report = new Report(Title);
            var builder = new DimensionBuilder(doc, view, Q.DimensionType(doc, Choices.Value(f.String("tipo"))), Conv.Cm(s.MenorSegmentoCm));
            Tx.Run(doc, Title, () =>
            {
                var service = new ObjectDimensionService(doc, view, builder, report);
                foreach (Element e in elements)
                {
                    int n = service.Dimension(e, options);
                    report.Count("cotas criadas", n);
                    if (n > 0) report.Count("objetos cotados");
                }
            }, deleteFailingAnnotations: true);

            if (report.Warnings.Count > 0 || report.Get("cotas criadas") == 0) report.Show();
            return Result.Succeeded;
        }

        /// <summary>
        /// Trava a vista 3D (exigência do Revit para anotar em 3D). A vista {3D} padrão não pode
        /// ser travada sem nome: nesse caso é criada uma cópia nomeada, que passa a ser a vista ativa.
        /// </summary>
        public static View3D Locked3D(CommandContext ctx, View3D v3)
        {
            Document doc = ctx.Doc;
            View3D target = v3;
            Tx.Run(doc, "Travar vista 3D", () =>
            {
                if (!Lock(v3))
                {
                    target = doc.GetElement(v3.Duplicate(ViewDuplicateOption.Duplicate)) as View3D;
                    if (target == null) throw new UserMessageException("Não foi possível travar a vista 3D. Use \"Salvar orientação e travar vista\" e tente novamente.");
                    new ViewTools(doc).SetName(target, "DetalhaBIM - 3D cotado");
                    if (!Lock(target)) throw new UserMessageException("Não foi possível travar a vista 3D. Use \"Salvar orientação e travar vista\" e tente novamente.");
                }
            });
            if (target.Id != v3.Id)
            {
                try
                {
                    ctx.UiDoc.ActiveView = target;
                }
                catch
                {
                    // A vista criada continua disponível no navegador de projeto.
                }
            }
            return target;
        }

        private static bool Lock(View3D v)
        {
            try
            {
                if (v.IsLocked) return true;
                if (!v.CanBeLocked()) return false;
                v.SaveOrientationAndLock();
                return v.IsLocked;
            }
            catch
            {
                return false;
            }
        }
    }
}
