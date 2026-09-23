using System;
using Autodesk.Revit.UI;

namespace DetalhaBIM.Core
{
    public static class Dialogs
    {
        public static void Info(string title, string instruction, string content = null)
        {
            var td = new TaskDialog("DetalhaBIM")
            {
                Title = "DetalhaBIM - " + title,
                MainInstruction = instruction,
                MainContent = content ?? string.Empty,
                CommonButtons = TaskDialogCommonButtons.Ok,
            };
            td.Show();
        }

        public static void Warning(string title, string content)
        {
            var td = new TaskDialog("DetalhaBIM")
            {
                Title = "DetalhaBIM - " + title,
                MainInstruction = "Atenção",
                MainContent = content,
                MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                CommonButtons = TaskDialogCommonButtons.Ok,
            };
            td.Show();
        }

        public static void Error(string title, Exception ex)
        {
            var td = new TaskDialog("DetalhaBIM")
            {
                Title = "DetalhaBIM - " + title,
                MainInstruction = "Não foi possível concluir a operação",
                MainContent = ex.Message + "\n\nOs detalhes foram gravados no log (DetalhaBIM > Sobre > Abrir pasta de logs).",
                ExpandedContent = ex.ToString(),
                MainIcon = TaskDialogIcon.TaskDialogIconError,
                CommonButtons = TaskDialogCommonButtons.Ok,
            };
            td.Show();
        }

        public static bool Confirm(string title, string instruction, string content)
        {
            var td = new TaskDialog("DetalhaBIM")
            {
                Title = "DetalhaBIM - " + title,
                MainInstruction = instruction,
                MainContent = content,
                MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.No,
            };
            return td.Show() == TaskDialogResult.Yes;
        }
    }
}
