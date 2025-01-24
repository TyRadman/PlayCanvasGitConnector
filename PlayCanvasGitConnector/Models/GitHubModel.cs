using PlayCanvasGitConnector.LoggingServices;
using System.Diagnostics;

namespace PlayCanvasGitConnector.Models
{
    internal static class GitHubModel
    {
        internal static void PushToGitHub(string? projectFolder)
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
                RunGitCommand("add .");
                RunGitCommand("commit -m \"Automated sync from PlayCanvas\"");
                RunGitCommand("push -u origin master --force");
                Console.WriteLine("Project successfully pushed to GitHub!");
                LoggerService.Log("Project successfully pushed to GitHub!", LogType.Success);
            }
            catch (Exception ex)
            {
                LoggerService.Log($"An error occurred during Git operations: {ex.Message}", LogType.Success);
            }
        }

        private static void RunGitCommand(string arguments)
        {
            Console.WriteLine($"Running Git command: git {arguments}");

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
            process.WaitForExit();

            if (process.ExitCode != 0 && output == "nothing to commit, working tree clean")
            {
                Console.WriteLine("Nothing to commit, working tree clean");
                return;
            }

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Git Command Output: {output}");
                Console.WriteLine($"Git Command Error: {error}");
                throw new Exception($"Git command failed: {error}");
            }

            Console.WriteLine($"Git Command Output: {output}");
        }

    }
}
