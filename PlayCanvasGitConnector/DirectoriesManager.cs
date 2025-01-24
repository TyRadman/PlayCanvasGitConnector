using DotNetEnv;
using System.IO;

namespace PlayCanvasGitConnector
{
    internal static class DirectoriesManager
    {
        internal static string OutputFolder { get; set; } = string.Empty;
        internal static string ProjectFolder { get; set; } = string.Empty;
        internal static string AppFolder { get; set; } = string.Empty;
        private const string OUTPUT_FOLDER = "OUTPUT_FOLDER";
        private const string GIT_FOLDER = "GIT_FOLDER";

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
