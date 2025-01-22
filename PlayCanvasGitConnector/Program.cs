using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using DotNetEnv;

namespace PlayCanvasGitConnector
{
    class Program
    {
        private const string API_BASE_URL = "https://playcanvas.com/api";
        private const string PLAYCANVAS_BRANCH_ID = "6c77b015-f2c6-4f3c-ad68-9e7a9292cfb0";
        private const string APP_NAME = "Playroom";

        // env variables declaration
        private const string OUTPUT_FOLDER = "OUTPUT_FOLDER";
        private const string GIT_FOLDER = "GIT_FOLDER";
        private const string PLAYCANVAS_TOKEN = "PLAYCANVAS_TOKEN";
        private const string PLAYCANVAS_PROJECT_ID = "PLAYCANVAS_PROJECT_ID";
        private const string SCENE_ID = "SCENE_ID";

        private static string? _outputFolder;
        private static string? _gitFolder;

        public static Action<string, LogType>? OnStatusUpdated { get; set; }
        public static Action? OnProcessFinished { get; set; }

        [STAThread]
        static void Main(string[] args)
        {
            if(args.Length > 0)
            {

                return;
            }

            var app = new Application();
            app.Run(new MainWindow());

            OnStatusUpdated += PrintLine;
        }

        public static PlayCanvasPushContext GetEnvContext()
        {
            string? pathToEnv = $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName}\\.env";

            if (pathToEnv == null)
            {
                throw new Exception("Path to .env file not found.");
            }

            Env.Load(pathToEnv);

            PlayCanvasPushContext context = new PlayCanvasPushContext
            {
                APIKeyToken = Environment.GetEnvironmentVariable(PLAYCANVAS_TOKEN),
                ProjectId = Environment.GetEnvironmentVariable(PLAYCANVAS_PROJECT_ID),
                BranchID = PLAYCANVAS_BRANCH_ID,
                SceneIDs = new[] { Environment.GetEnvironmentVariable(SCENE_ID) }
            };

            return context;
        }

        public async static void StartPushingProcess(PlayCanvasPushContext context)
        {
            if(!context.IsValid())
            {
                OnStatusUpdated?.Invoke("Invalid context provided.", LogType.Error);
                await Task.Delay(500);
                OnProcessFinished?.Invoke();
                return;
            }

            // cache directories
            _outputFolder = Environment.GetEnvironmentVariable(OUTPUT_FOLDER);
            _gitFolder = Environment.GetEnvironmentVariable(GIT_FOLDER);

            try
            {
                // start the export job
                OnStatusUpdated?.Invoke("Starting export job...", LogType.Info);
                var jobId = await StartExportJob(context);

                // poll for job status
                OnStatusUpdated?.Invoke("Waiting for export to complete...", LogType.Info);
                var downloadUrl = await PollForJobCompletion(jobId, context);

                // download the exported app
                OnStatusUpdated?.Invoke("Downloading exported app...", LogType.Info);
                var zipFilePath = await DownloadApp(downloadUrl);

                // extract the ZIP file and delete it
                OnStatusUpdated?.Invoke("Extracting app...", LogType.Info);
                ExtractZip(zipFilePath);

                OnStatusUpdated?.Invoke($"App successfully exported to '{_outputFolder}'!", LogType.Success);

                // push the project to GitHub
                OnStatusUpdated?.Invoke("Pushing project to GitHub...", LogType.Info);
                PushToGitHub(_gitFolder);
                OnStatusUpdated?.Invoke("Job finished successfully!", LogType.Success);
            }
            catch (Exception ex)
            {
                OnStatusUpdated?.Invoke($"An error occurred: {ex.Message}", LogType.Error);
            }

            OnProcessFinished?.Invoke();
        }

        private static async Task<int> StartExportJob(PlayCanvasPushContext context)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {context.APIKeyToken}");

            var requestBody = new
            {
                project_id = context.ProjectId,
                scenes = context.SceneIDs,
                name = APP_NAME,
                branch_id = PLAYCANVAS_BRANCH_ID
            };

            // example of request Body from PlayCanvas API documentation. We perform a POST request to the API endpoint with the request body
            //{"project_id": "your_project_id_here", "scenes": ["your_scene_id_here"], "name": "Playroom", "branch_id": "your_branch_id_here"}
            var response = await client.PostAsync($"{API_BASE_URL}/apps/download",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            // the response comes in this form. We cache and return the id to check on the status of the process
            // {"id": 123456,"status": "running","messages": [],"created_at": "2025-01-20T23:11:32.497Z","modified_at": "2025-01-20T23:11:32.497Z","data": {"owner_id": someValue,"project_id": someValue,"branch_id": "someValue","name": "project_name", "scenes": ["scene_number"]}}
            var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Console.WriteLine($"{responseJson.RootElement}");

            return responseJson.RootElement.GetProperty("id").GetInt32();
        }

        private static async Task<string> PollForJobCompletion(int jobId, PlayCanvasPushContext context)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {context.APIKeyToken}");

            while (true)
            {
                // we use the current job's id to check on the status of the job
                var response = await client.GetAsync($"{API_BASE_URL}/jobs/{jobId}");
                response.EnsureSuccessStatusCode();

                var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var status = responseJson.RootElement.GetProperty("status").GetString();

                string? downloadUrl = string.Empty;

                try
                {
                    downloadUrl = responseJson.RootElement.GetProperty("data").GetProperty("download_url").GetString();
                }
                catch
                {
                    Console.WriteLine("Download URL not available yet.");
                }
                Console.WriteLine($"Job status: {downloadUrl}");

                // once the job is complete, the downloadURL value will no longer be null and we get the download URL
                if (status == "complete" && downloadUrl != null)
                {
                    return downloadUrl;
                }
                else if (status == "error")
                {
                    throw new Exception("Export job failed.");
                }

                // wait for 2 seconds before polling again
                await Task.Delay(2000);
            }
        }

        private static async Task<string> DownloadApp(string downloadUrl)
        {
            using var client = new HttpClient();
            var zipFilePath = $"{APP_NAME}.zip";

            // now that we know the job is completed, we can download the file
            var response = await client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            await using var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);

            return zipFilePath;
        }

        private static void ExtractZip(string zipFilePath)
        {
            if (_outputFolder == null)
            {
                throw new Exception("Output folder directory not set.");
            }

            if (Directory.Exists(_outputFolder))
            {
                Directory.Delete(_outputFolder, true);
            }

            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            ZipFile.ExtractToDirectory(zipFilePath, _outputFolder);
        }

        private static void PushToGitHub(string? projectFolder)
        {
            if (projectFolder == null)
            {
                throw new Exception("Project folder not set.");
            }

            // Navigate to the project folder
            Environment.CurrentDirectory = projectFolder;
            Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");

            try
            {
                RunGitCommand("add .");
                RunGitCommand("commit -m \"Automated sync from PlayCanvas\"");
                RunGitCommand("push -u origin master --force");
                Console.WriteLine("Project successfully pushed to GitHub!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during Git operations: {ex.Message}");
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


        private static void PrintLine(string line, LogType logType = LogType.Info)
        {
            Console.WriteLine(line);
        }

        #region Event Handling
        public void RegisterForLog(Action<string, LogType> logAction)
        {
            OnStatusUpdated += logAction;
        }

        public void UnregisterForLog(Action<string, LogType> logAction)
        {
            OnStatusUpdated -= logAction;
        }

        public void RegisterForProcessCompleted(Action processFinishedAction)
        {
            OnProcessFinished += processFinishedAction;
        }

        public void UnregisterForProcessFinished(Action processFinishedAction)
        {
            OnProcessFinished -= processFinishedAction;
        }
        #endregion
    }

    public enum LogType
    {
        Info,
        Success,
        Error
    }
}