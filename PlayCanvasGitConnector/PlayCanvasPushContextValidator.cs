using System.Text;

namespace PlayCanvasGitConnector
{
    internal static class PlayCanvasPushContextValidator
    {
        internal static string Validate(PlayCanvasPushContext context)
        {
            StringBuilder report = new StringBuilder();

            if (string.IsNullOrEmpty(context.APIKeyToken))
            {
                report.Append("API Key Token is missing.\n");
            }

            if (string.IsNullOrEmpty(context.ProjectId))
            {
                report.Append("Project ID is missing.\n");
            }

            if (string.IsNullOrEmpty(context.FileDirectory))
            {
                report.Append("File Directory is missing.\n");
            }

            if (string.IsNullOrEmpty(context.RemoteGitURL))
            {
                report.Append("Remote Git URL is missing.\n");
            }

            return report.ToString();
        }
    }
}
