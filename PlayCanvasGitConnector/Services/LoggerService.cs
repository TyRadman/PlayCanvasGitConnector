using System.IO;
using System.Text;

namespace PlayCanvasGitConnector.LoggingServices
{
    internal class LoggerService
    {
        public static Action<string, LogType>? OnStatusUpdated { get; set; }

        private static string _log = string.Empty;

        internal static void Initialize()
        {
            Program.OnProcessFinished += SaveLog;
            OnStatusUpdated += PrintLine;
        }

        internal static void Log(string message, LogType logType)
        {
            OnStatusUpdated?.Invoke(message, logType);
        }

        private static void PrintLine(string line, LogType logType = LogType.Info)
        {
            _log = $"{_log}\n{line}";
            Console.WriteLine(line);
        }


        private static void SaveLog()
        {
            OnStatusUpdated?.Invoke("Saving log...", LogType.Info);
            var log = new StringBuilder();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            log.AppendLine($"Log generated at: {timestamp}\n{_log}");
            string logFilePath = Path.Combine(DirectoriesManager.ProjectFolder, "log.txt");

            if (File.Exists(logFilePath))
            {
                log.AppendLine($"\n\n{File.ReadAllText(logFilePath)}");
            }

            File.WriteAllText(logFilePath, log.ToString());
            OnStatusUpdated?.Invoke("Saved log successfully", LogType.Success);
        }

        internal static void RegisterForLog(Action<string, LogType> logAction)
        {
            OnStatusUpdated += logAction;
        }

        internal static void UnregisterForLog(Action<string, LogType> logAction)
        {
            OnStatusUpdated -= logAction;
        }
    }

    public enum LogType
    {
        Info,
        Success,
        Error
    }
}
