using PlayCanvasGitConnector.LoggingServices;
using System.IO;

namespace PlayCanvasGitConnector.Services
{
    internal static class FileServices
    {
        internal static void WriteCacheFile(string path, string content)
        {
            string cachePath = Path.Combine(path, "cache.appcache");
            File.WriteAllText(cachePath, content);
        }

        internal static string ReadCacheFile(string path)
        {
            string cachePath = Path.Combine(path, "cache.appcache");

            if (!Path.Exists(cachePath))
            {
                LoggerService.Log($"Cache file not found at {cachePath}", LogType.Error);
                return string.Empty;
            }

            return File.ReadAllText(cachePath);
        }
    }
}
