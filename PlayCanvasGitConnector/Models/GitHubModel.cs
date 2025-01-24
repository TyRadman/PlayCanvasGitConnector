using PlayCanvasGitConnector.LoggingServices;
using System.Diagnostics;

namespace PlayCanvasGitConnector.Models
{
    internal static class GitHubModel
    {
        internal static async void PushToGitHub(string? projectFolder, CancellationTokenSource cancellationTokenSource)
        {
            LoggerService.Log($"Pushing local repo at {projectFolder} project to GitHub...", LogType.Info);

            if (projectFolder == null)
            {
                throw new Exception("Project folder not set.");
            }

            // Navigate to the project folder
            Environment.CurrentDirectory = projectFolder;

            LoggerService.Log($"Current directory: {Environment.CurrentDirectory}", LogType.Info);

            try
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();

                await RunGitCommand("add .");
                await RunGitCommand("commit -m \"Automated sync from PlayCanvas\"");
                await RunGitCommand("push -u origin master --force");
                LoggerService.Log("Project successfully pushed to GitHub!", LogType.Success);
            }
            catch (Exception ex)
            {
                LoggerService.Log($"An error occurred during Git operations: {ex.Message}", LogType.Success);
            }
        }

        private static async Task RunGitCommand(string arguments)
        {
            LoggerService.Log($"Running Git command: git {arguments}", LogType.Info);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && output == "nothing to commit, working tree clean")
            {
                LoggerService.Log("Nothing to commit, working tree clean", LogType.Success);
                return;
            }

            if (process.ExitCode != 0)
            {
                LoggerService.Log($"Git Command Output: {output}", LogType.Error);
                LoggerService.Log($"Git Command Error: {error}", LogType.Error);
                throw new Exception($"Git command failed: {error}");
            }

            LoggerService.Log($"Git Command Output: {output}", LogType.Info);
        }

    }
}
