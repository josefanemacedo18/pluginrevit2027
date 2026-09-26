using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Interiores
{
    /// <summary>
    /// Acabamentos: escolha o material e clique nas faces (paredes, pisos, forros) ou dentro dos
    /// ambientes. Aplica como pintura ou cria o revestimento modelado sobre a parede.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AcabamentosCommand : CommandBase
    {
        protected override string Title => "Acabamentos";

        private const string Remove = "<Remover pintura>";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            View view = ctx.ActiveView;
            List<string> materials = ProjectChoices.Materials(doc, Remove).Where(n => n != Choices.Default).ToList();

            var dlg = new OptionsDialog("acabamentos", Title,
                "Escolha o material e clique nas faces que devem recebê-lo (paredes, pisos, forros) ou dentro dos ambientes para aplicar em todas as paredes. Continue clicando; ESC encerra.",
                Theme.Interiores, ctx.MainWindow, "Começar a aplicar");
            FormBuilder f = dlg.Form;
            f.Section("Acabamento");
            f.Radio("tipo", null, new[]
            {
                "Pintura — aplica o material na face (tinta, textura, papel de parede...)",
                "Revestimento modelado — cria a camada sobre a parede (cerâmica, porcelanato, painel), com portas e janelas recortadas",
            }, 0);
            f.Combo("material", "Material (digite um nome para criar)", materials, "Pintura acrílica branca", editable: true);
            f.Combo("cor", "Cor de um material novo", FinishService.Colors.Select(c => c.name).ToList(), FinishService.Colors[0].name);
            f.Section("Revestimento modelado");
            f.Number("espessura", "Espessura", 1, "cm");
            f.Radio("altura", "Altura", new[] { "Até o forro (ou até o topo do ambiente/parede)", "Altura definida abaixo" }, 1);
            f.Number("h", "Altura do revestimento", 120, "cm");
            f.Check("piso", "Começar no piso acabado (quando houver piso no ambiente)", true);
            f.Number("desloc", "Deslocamento da base (ex.: altura do rodapé)", 0, "cm");
            f.Section("Onde aplicar");
            f.Radio("onde", null, new[]
            {
                "Clicar nas faces, uma a uma — melhor em vistas 3D, cortes e elevações",
                "Clicar dentro dos ambientes (em planta): todas as paredes do ambiente",
            }, 0);
            f.Check("pPiso", "Ambientes + pintura: pintar também o piso", false);
            f.Check("pTeto", "Ambientes + pintura: pintar também o teto/forro", false);
            f.Check("quadro", "Ambientes: preencher o quadro de acabamentos (parede/piso/teto)", true);
            f.Hint("A pintura aparece nos estilos visuais Sombreado, Cores consistentes e Realista (3D, cortes e elevações).");
            if (!dlg.Run()) return Result.Cancelled;

            bool clad = f.Index("tipo") == 1;
            bool byRoom = f.Index("onde") == 1;
            string matName = f.String("material");
            bool remove = !clad && matName == Remove;
            if (string.IsNullOrWhiteSpace(matName) || (clad && matName == Remove))
                throw new UserMessageException("Escolha ou digite o material do acabamento.");
            if (byRoom && !(view is ViewPlan))
                throw new UserMessageException("Para clicar dentro dos ambientes, abra uma planta. Para clicar nas faces, escolha a outra opção.");

            var report = new Report(Title);
            Color color = FinishService.Colors.FirstOrDefault(c => c.name == f.String("cor")).color ?? FinishService.Colors[0].color;
            Material mat = null;
            WallType cladType = null;
            double thickness = Conv.Cm(f.Double("espessura"));
            if (clad && thickness < Conv.Mm(1)) throw new UserMessageException("Informe a espessura do revestimento.");
            Tx.Run(doc, Title + " - material", () =>
            {
                if (!remove) mat = Q.Material(doc, matName, true, report, color);
                if (clad) cladType = BaseboardService.EnsureWallType(doc, thickness, mat, "Revestimento");
            });

            var cladding = new BaseboardOptions
            {
                WallTypeId = cladType?.Id,
                Thickness = cladType?.Width ?? thickness,
                Height = Conv.Cm(f.Double("h")),
                ToCeiling = f.Index("altura") == 0,
                OnFloor = f.Bool("piso"),
                BaseOffset = Conv.Cm(f.Double("desloc")),
                CutAtDoors = true,
                CutAtLowWindows = false,
                AroundColumns = true,
                SkipExisting = true,
                Holes = true,
                Label = "REVESTIMENTO",
            };
            var finish = new FinishService(doc, report);
            var strips = new BaseboardService(doc, report);
            Selection sel = ctx.UiDoc.Selection;

            if (!byRoom)
            {
                while (true)
                {
                    Reference r;
                    try
                    {
                        r = sel.PickObject(ObjectType.Face, clad ? "Clique na face da parede que recebe o revestimento (ESC encerra)" : "Clique na face a pintar (ESC encerra)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }
                    Tx.Run(doc, Title, () =>
                    {
                        Element e = doc.GetElement(r);
                        if (clad)
                        {
                            if (e is Wall host && host.GetGeometryObjectFromReference(r) is PlanarFace pf)
                            {
                                int n = strips.CladFace(host, pf, cladding);
                                report.Count("revestimentos criados", n);
                            }
                            else
                            {
                                report.Warn("Revestimento modelado: clique na face plana de uma parede (para pisos e forros use Pintura ou Paginação de Piso).");
                            }
                        }
                        else if (finish.PaintFace(r, mat, remove))
                        {
                            report.Count(remove ? "faces com pintura removida" : "faces pintadas");
                        }
                    });
                }
            }
            else
            {
                Guards.EnsureWorkPlane(doc, view);
                while (true)
                {
                    XYZ p;
                    try
                    {
                        p = sel.PickPoint(ObjectSnapTypes.None, "Clique dentro do ambiente (ESC encerra)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }
                    Room room = RoomGeo.At(doc, p, view.GenLevel?.Id);
                    if (room == null)
                    {
                        report.Warn("Um dos cliques não caiu dentro de um ambiente delimitado.");
                        continue;
                    }
                    Tx.Run(doc, Title + " " + RoomGeo.Label(room), () =>
                    {
                        if (clad)
                        {
                            int n = strips.Create(room, cladding);
                            if (n > 0)
                            {
                                report.Count("trechos de revestimento", n);
                                report.Count("ambientes revestidos");
                            }
                        }
                        else
                        {
                            int n = finish.PaintRoom(room, mat, remove, f.Bool("pPiso"), f.Bool("pTeto"));
                            report.Count(remove ? "faces com pintura removida" : "faces pintadas", n);
                            if (n > 0) report.Count("ambientes pintados");
                        }
                        if (f.Bool("quadro") && mat != null)
                        {
                            finish.SetRoomFinishes(room,
                                !clad && f.Bool("pPiso") ? mat.Name : null,
                                mat.Name,
                                !clad && f.Bool("pTeto") ? mat.Name : null,
                                null);
                        }
                    });
                }
            }

            if (strips.Area > 0) report.Info($"Área de revestimento: {Conv.Format(Conv.ToM2(strips.Area), 2)} m² (sem descontar os vãos).");
            if (report.Total > 0 || report.Warnings.Count > 0) report.Show();
            return Result.Succeeded;
        }
    }
}
