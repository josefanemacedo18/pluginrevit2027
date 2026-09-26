using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Interiores
{
    /// <summary>Rodapés automáticos pelo contorno dos ambientes, interrompidos nas portas.</summary>
    [Transaction(TransactionMode.Manual)]
    public class RodapesCommand : CommandBase
    {
        protected override string Title => "Rodapés";

        private const string Auto = "<Criar automaticamente (espessura e material abaixo)>";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            List<string> wallTypes = new[] { Auto }.Concat(Q.All<WallType>(doc).Where(t => t.Kind == WallKind.Basic).Select(Q.Label).OrderBy(n => n)).ToList();
            List<string> sweepTypes = ProjectChoices.SweepTypes(doc);

            var dlg = new OptionsDialog("rodapes", Title,
                "Cria o rodapé de cada ambiente seguindo o contorno de acabamento das paredes, interrompido nas portas e contornando pilares.",
                Theme.Interiores, ctx.MainWindow, "Criar rodapés");
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions, 0);
            f.Section("Método");
            f.Radio("metodo", null, new[]
            {
                "Parede de rodapé — recomendado: corta nas portas, contorna pilares e cantos",
                "Perfil de parede (Wall Sweep) — usa um perfil de rodapé do projeto",
            }, 0);
            f.Section("Rodapé");
            f.Number("altura", "Altura", 7, "cm");
            f.Number("espessura", "Espessura", 1.5, "cm");
            f.Combo("material", "Material (digite para criar um novo)", ProjectChoices.Materials(doc, Choices.Default), Choices.Default, editable: true);
            f.Combo("tipoParede", "Tipo de parede", wallTypes, Auto);
            f.Combo("tipoPerfil", "Tipo de perfil (método Perfil)", sweepTypes.Count > 0 ? sweepTypes : new List<string> { Choices.None }, sweepTypes.FirstOrDefault() ?? Choices.None);
            f.Section("Base e interrupções");
            f.Check("sobrePiso", "Apoiar sobre o piso modelado do ambiente (quando existir)", true);
            f.Number("desloc", "Deslocamento adicional da base", 0, "cm");
            f.Check("portas", "Interromper nas portas", true);
            f.Check("janelas", "Interromper em janelas/portas-janelas com peitoril abaixo do topo do rodapé", true);
            f.Check("pilares", "Contornar pilares e outros elementos delimitadores", true);
            f.Check("pular", "Ignorar ambientes que já têm rodapé do DetalhaBIM", true);
            if (!dlg.Run()) return Result.Cancelled;

            bool sweep = f.Index("metodo") == 1;
            ElementType sweepType = sweep ? Q.TypeByLabel(Q.SweepTypes(doc), f.String("tipoPerfil")) : null;
            if (sweep && sweepType == null)
                throw new UserMessageException("O projeto não possui tipos de perfil de parede (Arquitetura > Parede > Perfil). Carregue um perfil de rodapé ou use o método \"Parede de rodapé\".");

            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"));
            var report = new Report(Title);
            var o = new BaseboardOptions
            {
                UseSweep = sweep,
                SweepTypeId = sweepType?.Id,
                Height = Conv.Cm(f.Double("altura")),
                Thickness = Conv.Cm(f.Double("espessura")),
                BaseOffset = Conv.Cm(f.Double("desloc")),
                OnFloor = f.Bool("sobrePiso"),
                CutAtDoors = f.Bool("portas"),
                CutAtLowWindows = f.Bool("janelas"),
                AroundColumns = f.Bool("pilares"),
                SkipExisting = f.Bool("pular"),
            };
            if (o.Height <= Conv.Mm(5) || (!sweep && o.Thickness <= Conv.Mm(1)))
                throw new UserMessageException("Informe altura e espessura maiores que zero.");

            var service = new BaseboardService(doc, report);
            Tx.Run(doc, Title, () =>
            {
                if (!sweep)
                {
                    WallType wt = Q.TypeByLabel(Q.All<WallType>(doc), f.String("tipoParede"));
                    if (wt == null || f.String("tipoParede") == Auto)
                    {
                        Material mat = Q.Material(doc, Choices.Value(f.String("material")), true, report);
                        wt = BaseboardService.EnsureWallType(doc, o.Thickness, mat);
                    }
                    o.WallTypeId = wt.Id;
                    o.Thickness = wt.Width;
                }
                foreach (Room room in rooms)
                {
                    int n = service.Create(room, o);
                    if (n > 0)
                    {
                        report.Count(sweep ? "perfis de rodapé" : "trechos de rodapé", n);
                        report.Count("ambientes com rodapé");
                    }
                }
            });

            if (service.Length > 0) report.Info($"Comprimento total de rodapé: {Conv.Format(Conv.ToM(service.Length), 2)} m");
            report.Show();
            return Result.Succeeded;
        }
    }
}
