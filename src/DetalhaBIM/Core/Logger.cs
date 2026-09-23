using System;
using System.IO;
using System.Text;

namespace DetalhaBIM.Core
{
    /// <summary>Registro simples em %AppData%\DetalhaBIM\logs para diagnóstico de problemas.</summary>
    public static class Logger
    {
        private static readonly object Sync = new object();

        public static string AppDataFolder
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DetalhaBIM");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string LogFolder
        {
            get
            {
                string dir = Path.Combine(AppDataFolder, "logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static void Info(string message) => Write("INFO", message);

        public static void Warn(string message) => Write("AVISO", message);

        public static void Error(string context, Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine(context);
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                sb.AppendLine(e.GetType().FullName + ": " + e.Message);
                sb.AppendLine(e.StackTrace);
            }
            Write("ERRO", sb.ToString());
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    string file = Path.Combine(LogFolder, $"detalhabim-{DateTime.Now:yyyy-MM}.log");
                    File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // O log nunca deve interromper o trabalho do usuário.
            }
        }
    }
}
