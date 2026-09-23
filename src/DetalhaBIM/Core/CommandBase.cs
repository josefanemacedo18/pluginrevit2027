using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DetalhaBIM.Core
{
    /// <summary>Dados comuns disponíveis para todos os comandos.</summary>
    public class CommandContext
    {
        public CommandContext(ExternalCommandData data)
        {
            UiApp = data.Application;
            UiDoc = UiApp.ActiveUIDocument;
            Doc = UiDoc?.Document;
            ActiveView = UiDoc?.ActiveView;
            Settings = SettingsService.Current;
        }

        public UIApplication UiApp { get; }
        public UIDocument UiDoc { get; }
        public Document Doc { get; }
        public View ActiveView { get; }
        public DetalhaSettings Settings { get; }
        public IntPtr MainWindow => UiApp.MainWindowHandle;
    }

    /// <summary>
    /// Base dos comandos: trata cancelamento (ESC), erros inesperados e registro em log,
    /// para que cada ferramenta se concentre apenas na sua lógica.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public abstract class CommandBase : IExternalCommand
    {
        /// <summary>Título exibido nas mensagens da ferramenta.</summary>
        protected abstract string Title { get; }

        protected abstract Result Run(CommandContext ctx);

        /// <summary>Se falso, o comando funciona mesmo sem projeto aberto (ex.: configurações).</summary>
        protected virtual bool RequiresDocument => true;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (RequiresDocument && commandData.Application.ActiveUIDocument == null)
            {
                Dialogs.Warning(Title, "Abra um projeto antes de usar o DetalhaBIM.");
                return Result.Cancelled;
            }

            try
            {
                Logger.Info("Executando: " + Title);
                return Run(new CommandContext(commandData));
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (UserCancelledException)
            {
                return Result.Cancelled;
            }
            catch (UserMessageException ex)
            {
                Dialogs.Warning(Title, ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Logger.Error("Erro em " + Title, ex);
                Dialogs.Error(Title, ex);
                return Result.Failed;
            }
        }
    }

    /// <summary>Lançada quando o usuário cancela um diálogo do plugin.</summary>
    public class UserCancelledException : Exception
    {
    }

    /// <summary>Lançada para interromper o comando exibindo uma orientação ao usuário.</summary>
    public class UserMessageException : Exception
    {
        public UserMessageException(string message) : base(message)
        {
        }
    }
}
