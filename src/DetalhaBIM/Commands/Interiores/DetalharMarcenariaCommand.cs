using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Interiores
{
    /// <summary>Kit de detalhamento de marcenaria/mobiliário: planta, vistas, isométrico, cotas e prancha.</summary>
    [Transaction(TransactionMode.Manual)]
    public class DetalharMarcenariaCommand : CommandBase
    {
        protected override string Title => "Detalhar Marcenaria";

        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_Casework, BuiltInCategory.OST_Furniture, BuiltInCategory.OST_FurnitureSystems,
            BuiltInCategory.OST_SpecialityEquipment, BuiltInCategory.OST_GenericModel, BuiltInCategory.OST_PlumbingFixtures,
        };

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            DetalhaSettings s = ctx.Settings;

            var dlg = new OptionsDialog("detalhar-marcenaria", Title,
                "Selecione móveis de marcenaria, bancadas ou mobiliário: para cada peça são criadas planta, vista frontal, vista lateral e isométrico, recortados, cotados e montados em prancha.",
                Theme.Interiores, ctx.MainWindow, "Selecionar peças");
            FormBuilder f = dlg.Form;
            f.Section("Vistas");
            f.Check("planta", "Planta recortada", true);
            f.Check("frontal", "Vista frontal", true);
            f.Check("lateral", "Vista lateral (lado direito)", true);
            f.Check("iso", "Isométrico 3D (caixa de corte na peça, travado)", true);
            f.Radio("frente", "Vista frontal olhando para", new[] { "A face \"Frente\" da família (padrão do Revit)", "O lado oposto" }, 0);
            f.Radio("agrupar", "Um detalhamento para cada", new[] { "Tipo (peças iguais são detalhadas uma vez)", "Peça selecionada" }, 0);
            f.Section("Escala, modelos e cotas");
            f.Combo("escala", "Escala", Choices.Scales, "1:20", true);
            f.Combo("mPlanta", "Modelo da planta", ProjectChoices.Templates(doc, ViewType.FloorPlan), Choices.Or(s.Vistas.ModeloPlanta, Choices.None));
            f.Combo("mElev", "Modelo das vistas", ProjectChoices.Templates(doc, ViewType.Elevation, ViewType.Section), Choices.Or(s.Vistas.ModeloElevacao, Choices.None));
            f.Combo("m3d", "Modelo do isométrico", ProjectChoices.Templates(doc, ViewType.ThreeD), Choices.Or(s.Vistas.Modelo3D, Choices.None));
            f.Number("folga", "Folga do recorte", 15, "cm");
            f.Check("cotar", "Cotar largura, profundidade e altura em todas as vistas", true);
            f.Combo("tipo", "Tipo de cota", ProjectChoices.DimensionTypes(doc), Choices.Or(s.Cotas.TipoCota, Choices.Default));
            f.Check("ocultar", "Ocultar os marcadores de elevação nas plantas", true);
            f.Section("Prancha");
            f.Check("prancha", "Montar uma prancha por peça", true);
            f.Combo("carimbo", "Carimbo (folha)", ProjectChoices.Symbols(doc, BuiltInCategory.OST_TitleBlocks, false), s.Pranchas.Carimbo);
            if (!dlg.Run()) return Result.Cancelled;

            List<Element> picked = Pick.SelectedOrPick(ctx.UiDoc, PredicateFilter.Categories(Categories), "Selecione as peças a detalhar e clique em Concluir");
            if (picked.Count == 0) return Result.Cancelled;
            List<Element> pieces = f.Index("agrupar") == 0
                ? picked.GroupBy(e => e.GetTypeId()).Select(g => g.First()).ToList()
                : picked;

            int scale = Choices.ParseScale(f.String("escala"), 20);
            var o = new JoineryOptions
            {
                Plan = f.Bool("planta"),
                Front = f.Bool("frontal"),
                Side = f.Bool("lateral"),
                Isometric = f.Bool("iso"),
                FamilyFront = f.Index("frente") == 0,
                Dimension = f.Bool("cotar"),
                HideMarkers = f.Bool("ocultar"),
                Scale = scale,
                IsoScale = scale,
                PlanTemplate = Choices.Value(f.String("mPlanta")),
                ElevationTemplate = Choices.Value(f.String("mElev")),
                IsoTemplate = Choices.Value(f.String("m3d")),
                Margin = Conv.Cm(f.Double("folga")),
                DimensionType = Q.DimensionType(doc, Choices.Value(f.String("tipo"))),
                DimensionOffset = s.Cotas.PrimeiraLinhaMm,
            };

            var report = new Report(Title);
            var vt = new ViewTools(doc);
            var service = new JoineryDetailService(doc, vt, report);
            var sheets = new SheetService(doc, report, s.Pranchas);
            FamilySymbol tb = f.Bool("prancha") ? sheets.TitleBlock(f.String("carimbo")) : null;
            var names = new HashSet<string>();

            using (var group = new TransactionGroup(doc, "DetalhaBIM - " + Title))
            {
                group.Start();
                foreach (Element e in pieces)
                {
                    string name = JoineryDetailService.PieceName(e);
                    string unique = name;
                    for (int i = 2; !names.Add(unique); i++) unique = $"{name} ({i})";
                    try
                    {
                        Tx.Run(doc, Title + " " + unique, () =>
                        {
                            List<View> views = service.Create(e, unique, o, ctx.ActiveView);
                            if (views.Count > 0) report.Count("peças detalhadas");
                            if (tb != null && views.Count > 0) report.Count("pranchas", sheets.Layout(views, tb, "MARCENARIA - " + unique).Count);
                        }, deleteFailingAnnotations: true);
                    }
                    catch (UserMessageException)
                    {
                        throw;
                    }
                    catch (System.Exception ex)
                    {
                        report.Warn($"{unique}: {ex.Message}");
                        Logger.Error("Detalhar marcenaria " + unique, ex);
                    }
                }
                group.Assimilate();
            }
            report.Show();
            return Result.Succeeded;
        }
    }
}
