using PlayCanvasGitConnector.LoggingServices;
using System.Diagnostics;
using System.IO;

namespace PlayCanvasGitConnector.Models
{
    internal static class GitHubModel
    {
        internal static async Task PushToGitHub(string? projectFolder, CancellationTokenSource cancellationTokenSource, PlayCanvasPushContext context)
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

                if(!Directory.Exists(Path.Combine(projectFolder, ".git")))
                {
                    await RunGitCommand("init");
                    await RunGitCommand("remote add origin " + context.RemoteGitURL);
                }

                await RunGitCommand("add .");
                await RunGitCommand("commit -m \"Automated sync from PlayCanvas\"");
                await RunGitCommand("push -u origin master --force");
                LoggerService.Log("Project successfully pushed to GitHub!", LogType.Success);
            }
            catch (Exception ex)
            {
                LoggerService.Log($"Git: {ex.Message}", LogType.Success);
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

            if (process.ExitCode != 0 && output.Contains("nothing to commit, working tree clean"))
            {
                LoggerService.Log("Nothing to commit, working tree clean", LogType.Success);
                throw new Exception($"Nothing to commit, working tree clean");
            }

            if (process.ExitCode != 0)
            {
                LoggerService.Log($"Git Command Output 1: {output}", LogType.Warning);
                LoggerService.Log($"Git Command Error: {error}", LogType.Error);
                throw new Exception($"Git command failed: {error}");
            }

            if (!string.IsNullOrEmpty(output))
            {
                LoggerService.Log($"Git Command Output: {output}", LogType.Info);
            }
        }

        internal static string GetRemoteRepository(string directoryPath)
        {
            LoggerService.Log("Getting remote repository...", LogType.Info);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "config --get remote.origin.url",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = directoryPath
                }
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                LoggerService.Log($"Git Command Output: {output}", LogType.Warning);
                LoggerService.Log($"Git Command Error: {error}", LogType.Error);
                throw new Exception($"Git command failed: {error}");
            }

            LoggerService.Log($"Remote repository: {output}", LogType.Info);
            return output.Remove(output.Length - 1);
        }
    }
}
