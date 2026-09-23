using System;
using Autodesk.Revit.UI;
using DetalhaBIM.Core;
using DetalhaBIM.Ribbon;

namespace DetalhaBIM
{
    /// <summary>
    /// Ponto de entrada do DetalhaBIM no Revit 2027: cria a aba com os painéis Cotas, Vistas,
    /// Documentação, Organização, Modelagem e DetalhaBIM.
    /// </summary>
    public class App : IExternalApplication
    {
        public static string Version => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                Logger.Info($"Iniciando DetalhaBIM {Version} (Revit {application.ControlledApplication.VersionNumber}, build {application.ControlledApplication.VersionBuild}).");
                RibbonBuilder.Build(application);
                Logger.Info($"DetalhaBIM {Version} carregado no Revit {application.ControlledApplication.VersionNumber}.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("Falha ao iniciar o DetalhaBIM", ex);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
    }
}
