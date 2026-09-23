using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;

namespace DetalhaBIM.Ribbon
{
    /// <summary>Monta a aba "DetalhaBIM" na faixa de opções a partir do catálogo de ferramentas.</summary>
    public static class RibbonBuilder
    {
        public const string TabName = "DetalhaBIM";
        public const string HelpUrl = "https://github.com/josefanemacedo18/pluginrevit2027/blob/main/docs/MANUAL.md";

        public static void Build(UIControlledApplication app)
        {
            try
            {
                app.CreateRibbonTab(TabName);
            }
            catch
            {
                // A aba já existe (ex.: recarregamento).
            }

            string assembly = typeof(RibbonBuilder).Assembly.Location;
            foreach (PanelDef panelDef in ToolCatalog.Panels)
            {
                RibbonPanel panel = app.CreateRibbonPanel(TabName, panelDef.Name);
                var pending = new List<PushButtonData>();

                foreach (ToolDef tool in ToolCatalog.Tools.Where(t => t.Panel == panelDef.Name))
                {
                    PushButtonData data = Data(tool, panelDef, assembly);
                    if (tool.Small)
                    {
                        pending.Add(data);
                        if (pending.Count == 3) Flush(panel, pending);
                    }
                    else
                    {
                        Flush(panel, pending);
                        panel.AddItem(data);
                    }
                }
                Flush(panel, pending);
            }
        }

        private static PushButtonData Data(ToolDef tool, PanelDef panel, string assembly)
        {
            var data = new PushButtonData("DetalhaBIM_" + tool.Id, tool.Text, assembly, tool.Command.FullName)
            {
                ToolTip = tool.Tooltip,
                LongDescription = tool.Description,
                LargeImage = Icons.Get(tool.Icon, panel.Color, 32),
                Image = Icons.Get(tool.Icon, panel.Color, 16),
                AvailabilityClassName = tool.Availability?.FullName,
            };
            try
            {
                data.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, HelpUrl));
            }
            catch (Exception ex)
            {
                Logger.Error("Ajuda contextual", ex);
            }
            return data;
        }

        private static void Flush(RibbonPanel panel, List<PushButtonData> pending)
        {
            switch (pending.Count)
            {
                case 0:
                    return;
                case 1:
                    panel.AddItem(pending[0]);
                    break;
                case 2:
                    panel.AddStackedItems(pending[0], pending[1]);
                    break;
                default:
                    panel.AddStackedItems(pending[0], pending[1], pending[2]);
                    break;
            }
            pending.Clear();
        }
    }
}
