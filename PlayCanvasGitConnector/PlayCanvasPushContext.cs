
namespace PlayCanvasGitConnector
{
    internal class PlayCanvasPushContext
    {
        public string? APIKeyToken { get; set; }
        public string? ProjectId { get; set; }
        public string? BranchID { get; set; }
        public string[]? SceneIDs { get; set; }

        internal bool IsValid()
        {
            return !string.IsNullOrEmpty(APIKeyToken) && !string.IsNullOrEmpty(ProjectId) && !string.IsNullOrEmpty(BranchID) && SceneIDs != null && SceneIDs.Length > 0;
        }
    }
}
