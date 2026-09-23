using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Vistas
{
    /// <summary>
    /// Kit completo de detalhamento por ambiente: planta, forro, elevações internas e isométrico,
    /// já recortados, nomeados, com modelo de vista e — opcionalmente — cotados e em prancha.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class VistasAmbienteCommand : CommandBase
    {
        protected override string Title => "Vistas por Ambiente";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            VistasSettings vs = ctx.Settings.Vistas;

            var dlg = new OptionsDialog("vistas-ambiente", Title,
                "Gera, para cada ambiente, as vistas de detalhamento já recortadas, nomeadas e com modelo de vista: planta, forro, uma elevação por parede e isométrico 3D.",
                Theme.Vistas, ctx.MainWindow, "Criar vistas", 580);
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions, 0);
            f.Section("Vistas a criar");
            f.Check("planta", "Planta baixa do ambiente", true);
            f.Check("forro", "Planta de forro do ambiente", true);
            f.Check("elevacoes", "Elevações internas (uma para cada parede)", true);
            f.Check("iso", "Isométrico 3D com caixa de corte", true);
            f.Section("Modelos de vista (View Templates)");
            f.Combo("mPlanta", "Planta", Choices.Templates(doc, ViewType.FloorPlan), Choices.Or(vs.ModeloPlanta, Choices.None));
            f.Combo("mForro", "Forro", Choices.Templates(doc, ViewType.CeilingPlan), Choices.Or(vs.ModeloForro, Choices.None));
            f.Combo("mElev", "Elevações", Choices.Templates(doc, ViewType.Elevation, ViewType.Section), Choices.Or(vs.ModeloElevacao, Choices.None));
            f.Combo("m3d", "Isométrico", Choices.Templates(doc, ViewType.ThreeD), Choices.Or(vs.Modelo3D, Choices.None));
            f.Section("Escalas");
            f.Combo("ePlanta", "Plantas", Choices.Scales, Choices.Scale(vs.EscalaPlanta), true);
            f.Combo("eElev", "Elevações", Choices.Scales, Choices.Scale(vs.EscalaElevacao), true);
            f.Combo("e3d", "Isométrico", Choices.Scales, Choices.Scale(vs.Escala3D), true);
            f.Section("Automatizar");
            f.Check("cotar", "Cotar a planta do ambiente (face a face + vãos)", true);
            f.Check("etiquetar", "Etiquetar ambiente, portas e janelas na planta", true);
            f.Check("cotarElev", "Cotar a largura de cada parede nas elevações", false);
            f.Check("separadores", "Não criar elevação em linhas separadoras de ambiente", true);
            f.Section("Pranchas");
            f.Check("prancha", "Montar uma prancha por ambiente com as vistas criadas", false);
            f.Combo("carimbo", "Carimbo (folha)", Choices.Symbols(doc, BuiltInCategory.OST_TitleBlocks, false),
                ctx.Settings.Pranchas.Carimbo);
            if (!dlg.Run()) return Result.Cancelled;

            var options = new RoomViewOptions
            {
                Plan = f.Bool("planta"),
                Ceiling = f.Bool("forro"),
                Elevations = f.Bool("elevacoes"),
                Isometric = f.Bool("iso"),
                PlanTemplate = Choices.Value(f.String("mPlanta")),
                CeilingTemplate = Choices.Value(f.String("mForro")),
                ElevationTemplate = Choices.Value(f.String("mElev")),
                IsoTemplate = Choices.Value(f.String("m3d")),
                PlanScale = Choices.ParseScale(f.String("ePlanta"), vs.EscalaPlanta),
                ElevationScale = Choices.ParseScale(f.String("eElev"), vs.EscalaElevacao),
                IsoScale = Choices.ParseScale(f.String("e3d"), vs.Escala3D),
                DimensionPlan = f.Bool("cotar"),
                TagPlan = f.Bool("etiquetar"),
                DimensionElevations = f.Bool("cotarElev"),
                SkipSeparationLines = f.Bool("separadores"),
            };

            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"));
            return Execute(ctx, rooms, options, f.Bool("prancha"), f.String("carimbo"), Title);
        }

        /// <summary>Executa a criação (compartilhado com os atalhos Isométrico e Elevações).</summary>
        public static Result Execute(CommandContext ctx, List<Room> rooms, RoomViewOptions options, bool sheets, string titleBlock, string title)
        {
            Document doc = ctx.Doc;
            var report = new Report(title);
            var vt = new ViewTools(doc);
            var svc = new RoomViewService(doc, vt, report, ctx.Settings);
            var sheetSvc = new SheetService(doc, report, ctx.Settings.Pranchas);
            FamilySymbol tb = sheets ? sheetSvc.TitleBlock(titleBlock) : null;

            using (var group = new TransactionGroup(doc, "DetalhaBIM - " + title))
            {
                group.Start();
                foreach (Room room in rooms)
                {
                    try
                    {
                        Tx.Run(doc, title + " " + RoomGeo.Label(room), () =>
                        {
                            List<View> views = svc.Create(room, options, ctx.ActiveView);
                            if (sheets && views.Count > 0)
                            {
                                string name = RoomGeo.FormatName(room, ctx.Settings.Vistas.PadraoNome, "DETALHAMENTO");
                                report.Count("pranchas", sheetSvc.Layout(views, tb, name).Count);
                            }
                        }, deleteFailingAnnotations: true);
                    }
                    catch (System.Exception ex)
                    {
                        report.Warn($"Ambiente {RoomGeo.Label(room)}: {ex.Message}");
                        Logger.Error("Vistas do ambiente " + RoomGeo.Label(room), ex);
                    }
                }
                group.Assimilate();
            }

            report.Count("ambientes processados", rooms.Count);
            report.Show();
            return Result.Succeeded;
        }
    }

    /// <summary>Atalho: isométrico 3D dos ambientes selecionados, com as configurações padrão.</summary>
    [Transaction(TransactionMode.Manual)]
    public class IsometricoCommand : CommandBase
    {
        protected override string Title => "Isométrico por Ambiente";

        protected override Result Run(CommandContext ctx)
        {
            VistasSettings vs = ctx.Settings.Vistas;
            List<Room> rooms = Pick.Rooms(ctx.UiDoc, RoomScope.Selecionar);
            var o = new RoomViewOptions
            {
                Plan = false,
                Ceiling = false,
                Elevations = false,
                Isometric = true,
                IsoTemplate = vs.Modelo3D,
                IsoScale = vs.Escala3D,
            };
            return VistasAmbienteCommand.Execute(ctx, rooms, o, false, null, Title);
        }
    }

    /// <summary>Atalho: elevações internas (uma por parede) dos ambientes selecionados.</summary>
    [Transaction(TransactionMode.Manual)]
    public class ElevacoesCommand : CommandBase
    {
        protected override string Title => "Elevações por Ambiente";

        protected override Result Run(CommandContext ctx)
        {
            VistasSettings vs = ctx.Settings.Vistas;
            List<Room> rooms = Pick.Rooms(ctx.UiDoc, RoomScope.Selecionar);
            var o = new RoomViewOptions
            {
                Plan = false,
                Ceiling = false,
                Elevations = true,
                Isometric = false,
                ElevationTemplate = vs.ModeloElevacao,
                ElevationScale = vs.EscalaElevacao,
                DimensionElevations = true,
            };
            return VistasAmbienteCommand.Execute(ctx, rooms, o, false, null, Title);
        }
    }
}
