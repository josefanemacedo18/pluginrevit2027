using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Services;
using DetalhaBIM.UI;

namespace DetalhaBIM.Commands.Interiores
{
    /// <summary>Pinta paredes, piso e teto de cada ambiente e preenche os parâmetros de acabamento.</summary>
    [Transaction(TransactionMode.Manual)]
    public class AcabamentosCommand : CommandBase
    {
        protected override string Title => "Acabamentos por Ambiente";

        private const string NoPaint = "<Não pintar>";
        private const string Remove = "<Remover pintura>";

        protected override Result Run(CommandContext ctx)
        {
            Document doc = ctx.Doc;
            List<string> materials = ProjectChoices.Materials(doc, NoPaint);
            materials.Insert(1, Remove);

            var dlg = new OptionsDialog("acabamentos", Title,
                "Aplica o material de acabamento nas faces que delimitam cada ambiente (como a ferramenta Pintura, sem mudar as paredes) e preenche os campos do quadro de acabamentos.",
                Theme.Interiores, ctx.MainWindow, "Aplicar");
            FormBuilder f = dlg.Form;
            f.Section("Ambientes");
            f.Radio("escopo", null, Pick.RoomScopeOptions, 0);
            f.Section("Pintar faces (digite um nome para criar o material)");
            f.Combo("mParede", "Paredes", materials, NoPaint, editable: true);
            f.Combo("mPiso", "Piso", materials, NoPaint, editable: true);
            f.Combo("mTeto", "Teto / forro", materials, NoPaint, editable: true);
            f.Hint("Se uma mesma face de parede atravessa vários ambientes, ela é pintada inteira — use \"Dividir face\" do Revit antes, se precisar de cores diferentes.");
            f.Section("Quadro de acabamentos (parâmetros do ambiente)");
            f.Check("params", "Preencher os acabamentos do ambiente", true);
            f.Text("tPiso", "Acabamento do piso", "", "Vazio = usa o nome do material do piso escolhido acima.");
            f.Text("tParede", "Acabamento das paredes", "", "Vazio = usa o nome do material das paredes escolhido acima.");
            f.Text("tTeto", "Acabamento do teto", "", "Vazio = usa o nome do material do teto escolhido acima.");
            f.Text("tRodape", "Rodapé", "");
            f.Check("manter", "Não substituir valores já preenchidos", false);
            if (!dlg.Run()) return Result.Cancelled;

            List<Room> rooms = Pick.Rooms(ctx.UiDoc, (RoomScope)f.Index("escopo"));
            var report = new Report(Title);
            Tx.Run(doc, Title, () =>
            {
                Material Mat(string key) => IsChoice(f.String(key)) ? null : Q.Material(doc, f.String(key), true, report);
                string Txt(string key, Material m) => string.IsNullOrWhiteSpace(f.String(key)) ? m?.Name : f.String(key);
                Material wall = Mat("mParede"), floor = Mat("mPiso"), ceiling = Mat("mTeto");
                var o = new FinishOptions
                {
                    Wall = wall,
                    Floor = floor,
                    Ceiling = ceiling,
                    RemoveWall = f.String("mParede") == Remove,
                    RemoveFloor = f.String("mPiso") == Remove,
                    RemoveCeiling = f.String("mTeto") == Remove,
                    SetParameters = f.Bool("params"),
                    KeepFilled = f.Bool("manter"),
                    TextFloor = Txt("tPiso", floor),
                    TextWall = Txt("tParede", wall),
                    TextCeiling = Txt("tTeto", ceiling),
                    TextBase = f.String("tRodape"),
                };
                var service = new FinishService(doc, report);
                foreach (Room r in rooms) service.Apply(r, o);
            });
            report.Show();
            return Result.Succeeded;
        }

        private static bool IsChoice(string s) => string.IsNullOrWhiteSpace(s) || s == NoPaint || s == Remove;
    }
}
