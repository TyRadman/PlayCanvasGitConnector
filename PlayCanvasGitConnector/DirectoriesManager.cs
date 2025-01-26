using System.IO;

namespace PlayCanvasGitConnector
{
    internal static class DirectoriesManager
    {
        internal static string OutputFolder { get; set; } = string.Empty;
        internal static string ProjectFolder { get; set; } = string.Empty;
        internal static string AppFolder { get; set; } = string.Empty;

        internal static void Initialize()
        {

        }

        internal static void SetOutputFolder(string path)
        {
            ProjectFolder = path;
            OutputFolder = Path.Combine(ProjectFolder, "PlayCanvasApp");
        }

        internal static bool HasValidDirectories()
        {
            return Directory.Exists(OutputFolder) && Directory.Exists(ProjectFolder);
        }
    }
}
