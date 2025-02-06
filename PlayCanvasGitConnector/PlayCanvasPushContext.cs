using PlayCanvasGitConnector.LoggingServices;

namespace PlayCanvasGitConnector
{
    internal class PlayCanvasPushContext
    {
        public string? APIKeyToken { get; set; }
        public string? ProjectId { get; set; }
        public string? BranchID { get; set; }
        public string[]? SceneIDs { get; set; }
        public string? FileDirectory { get; set; }
        public string? RemoteGitURL { get; set; }

        internal bool IsValid()
        {
            return !string.IsNullOrEmpty(APIKeyToken) && !string.IsNullOrEmpty(ProjectId) && SceneIDs != null && SceneIDs.Length > 0
                && !string.IsNullOrEmpty(FileDirectory) && !string.IsNullOrEmpty(RemoteGitURL);
        }

        internal void LogContext(LogType logType)
        {
            if (String.IsNullOrEmpty(APIKeyToken))
            {
                LoggerService.Log($"API Key Token: {APIKeyToken}", logType);
            }

            if(String.IsNullOrEmpty(ProjectId))
            {
                LoggerService.Log($"Project ID: {ProjectId}", logType);
            }
        }

        internal void LogContext()
        {
            LoggerService.Log($"API Key Token: {APIKeyToken}", LogType.Info);
            LoggerService.Log($"Project ID: {ProjectId}", LogType.Info);
            LoggerService.Log($"Branch ID: {BranchID}", LogType.Info);
            LoggerService.Log($"Scene IDs: {string.Join(" ", SceneIDs)}", LogType.Info);
        }
    }
}
