using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Modelagem
{
    /// <summary>Forros pelo contorno dos ambientes, com opção de sanca/rebaixo perimetral.</summary>
    [Transaction(TransactionMode.Manual)]
    public class ForrosCommand : CommandBase
    {
        protected override string Title => "Forros por Ambiente";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            ModelagemSettings ms = ctx.Settings.Modelagem;
            List<string> types = ProjectChoices.Types<CeilingType>(doc, false);
            if (types.Count == 0) throw new UserMessageException("O projeto não possui tipos de forro.");

            var dlg = new OptionsDialog("forros", Title,
                "Cria o forro de cada ambiente seguindo o contorno das paredes, na altura desejada, com opção de sanca (rebaixo perimetral).",
                Theme.Modelagem, ctx.MainWindow, "Criar forros");
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions, 0);
            f.Section("Forro");
            f.Combo("tipo", "Tipo de forro", types, Choices.Or(ms.TipoForro, types[0]));
            f.Number("altura", "Altura do forro (pé-direito)", ms.AlturaForroCm, "cm");
            f.Check("pular", "Ignorar ambientes que já possuem forro", true);
            f.Section("Sanca / rebaixo perimetral");
            f.Check("sanca", "Criar sanca perimetral", false);
            f.Number("largura", "Largura da sanca", ms.LarguraSancaCm, "cm");
            f.Number("rebaixo", "Rebaixo da sanca em relação ao forro", ms.RebaixoSancaCm, "cm");
            f.Combo("tipoSanca", "Tipo de forro da sanca", types, Choices.Or(ms.TipoForro, types[0]));
            f.Hint("A sanca é criada como um segundo forro, na faixa junto às paredes, mais baixo que o forro central.");
            if (!dlg.Run()) return Result.Cancelled;

            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"));
            List<CeilingType> all = Q.All<CeilingType>(doc);
            CeilingType main = Q.TypeByLabel(all, f.String("tipo"));
            CeilingType sanca = Q.TypeByLabel(all, f.String("tipoSanca")) ?? main;
            double height = Conv.Cm(f.Double("altura"));
            double width = Conv.Cm(f.Double("largura"));
            double drop = Conv.Cm(f.Double("rebaixo"));
            bool withSanca = f.Bool("sanca") && width > Conv.Cm(1);
            double tol = ctx.UiApp.Application.ShortCurveTolerance;
            var report = new Report(Title);

            List<Ceiling> existing = f.Bool("pular") ? Q.All<Ceiling>(doc) : new List<Ceiling>();

            Tx.Run(doc, Title, () =>
            {
                foreach (Room room in rooms)
                {
                    if (existing.Any(c => c.LevelId == room.LevelId && Inside(room, c)))
                    {
                        report.Count("ambientes que já tinham forro (ignorados)");
                        continue;
                    }
                    List<CurveLoop> loops = RoomGeo.Loops(room, tol);
                    if (loops.Count == 0)
                    {
                        report.Warn($"Ambiente {RoomGeo.Label(room)}: contorno inválido.");
                        continue;
                    }

                    bool ok = Tx.TrySub(doc, () =>
                    {
                        CurveLoop inner = withSanca ? Offset(loops[0], width) : null;
                        if (withSanca && inner == null)
                        {
                            report.Warn($"Ambiente {RoomGeo.Label(room)}: pequeno demais para a sanca — criado forro simples.");
                        }

                        if (inner != null)
                        {
                            Create(doc, new List<CurveLoop> { inner }, main, room, height);
                            Create(doc, new List<CurveLoop> { loops[0], inner }, sanca, room, height - drop);
                            report.Count("sancas");
                        }
                        else
                        {
                            Create(doc, loops, main, room, height);
                        }
                    });
                    if (ok) report.Count("forros criados");
                    else report.Warn($"Ambiente {RoomGeo.Label(room)}: o Revit não aceitou o contorno do forro.");
                }
            });

            report.Show();
            return Result.Succeeded;
        }

        private static void Create(Document doc, IList<CurveLoop> loops, CeilingType type, Room room, double height)
        {
            Ceiling c = Ceiling.Create(doc, loops, type.Id, room.LevelId);
            Q.Set(c, BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM, height + room.BaseOffset);
        }

        /// <summary>Desloca o contorno para dentro (testa os dois sentidos e fica com o de menor área).</summary>
        public static CurveLoop Offset(CurveLoop loop, double distance)
        {
            double area = Geo.LoopArea(loop);
            CurveLoop best = null;
            double bestArea = double.MaxValue;
            foreach (double d in new[] { distance, -distance })
            {
                try
                {
                    CurveLoop o = CurveLoop.CreateViaOffset(loop, d, XYZ.BasisZ);
                    double a = Geo.LoopArea(o);
                    if (a < area && a > 0.01 && a < bestArea)
                    {
                        best = o;
                        bestArea = a;
                    }
                }
                catch
                {
                    // Deslocamento impossível nesse sentido (ambiente estreito).
                }
            }
            return best;
        }

        private static bool Inside(Room room, Element e)
        {
            BoundingBoxXYZ bb = e.get_BoundingBox(null);
            return bb != null && RoomGeo.Contains(room, (bb.Min + bb.Max) / 2);
        }
    }
}
