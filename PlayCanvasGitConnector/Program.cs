using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
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

        private static string _log = string.Empty;
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public static Action<string, LogType>? OnStatusUpdated { get; set; }
        public static Action? OnProcessFinished { get; set; }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {

                return;
            }

            OnProcessFinished += SaveLog;
            OnStatusUpdated += PrintLine;
            var app = new Application();
            app.Run(new MainWindow());
        }

        public static PlayCanvasPushContext GetEnvContext()
        {

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
            string? pathToEnv = $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName}\\.env";

            if (pathToEnv == null)
            {
                throw new Exception("Path to .env file not found.");
            }

            Env.Load(pathToEnv);

            // cache directories
            _outputFolder = Environment.GetEnvironmentVariable(OUTPUT_FOLDER);
            _gitFolder = Environment.GetEnvironmentVariable(GIT_FOLDER);

            if (!context.IsValid())
            {
                OnStatusUpdated?.Invoke("Invalid context provided.", LogType.Error);
                await Task.Delay(500);
                OnProcessFinished?.Invoke();
                return;
            }

            try
            {
                // cancel the job if requested
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                // export the app
                await ExportAppFile(context);

                // download all scripts
                await DownloadAllScripts(context);

                // push the project to GitHub
                PushToGitHub(_gitFolder);

                // save the log
                SaveLog();
                
                OnStatusUpdated?.Invoke("Job finished successfully!", LogType.Success);
            }
            catch (Exception ex)
            {
                OnStatusUpdated?.Invoke($"An error occurred: {ex.Message}", LogType.Error);
            }

            OnProcessFinished?.Invoke();
        }

        private static void SaveLog()
        {
            OnStatusUpdated?.Invoke("Saving log...", LogType.Info);
            var log = new StringBuilder();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            log.AppendLine($"Log generated at: {timestamp}\n{_log}");
            string logFilePath = Path.Combine(Directory.GetParent(_outputFolder).ToString(), "log.txt");

            if (File.Exists(logFilePath))
            {
                log.AppendLine($"\n\n{File.ReadAllText(logFilePath)}");
            }

            File.WriteAllText(logFilePath, log.ToString());
            OnStatusUpdated?.Invoke("Saved log successfully", LogType.Success);
        }

        private static async Task<string> ExportAppFile(PlayCanvasPushContext context)
        {
            try
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                OnStatusUpdated?.Invoke("Starting export job...", LogType.Info);
                var jobId = await StartExportJob(context);

                // poll for job status
                OnStatusUpdated?.Invoke("Waiting for export to complete...", LogType.Info);
                var downloadUrl = await PollForJobCompletion(jobId, context);

                // download the exported app
                OnStatusUpdated?.Invoke("Downloading exported app...", LogType.Info);

                string zipFilePath = await DownloadApp(downloadUrl);

                // extract the ZIP file and delete it
                OnStatusUpdated?.Invoke("Extracting app...", LogType.Info);
                ExtractZip(zipFilePath);

                OnStatusUpdated?.Invoke($"App successfully exported to '{_outputFolder}'!", LogType.Success);
            }
            catch (OperationCanceledException)
            {
                OnStatusUpdated?.Invoke("Job cancelled.", LogType.Success);
            }
            catch (Exception ex)
            {
                OnStatusUpdated?.Invoke($"An error occurred: {ex.Message}", LogType.Success);
            }

            return string.Empty;
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
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

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

        public static async Task DownloadAllScripts(PlayCanvasPushContext context)
        {
            OnStatusUpdated?.Invoke("Downloading scripts...", LogType.Info);

            var assets = await GetAllAssets(context);

            // filter the scripts
            var scripts = assets.Where(asset => asset.GetProperty("type").GetString() == "script");

            foreach (var script in scripts)
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                await DownloadAsset(script, context);
            }

            OnStatusUpdated?.Invoke("All scripts downloaded.", LogType.Info);
        }

        private static async Task<List<JsonElement>> GetAllAssets(PlayCanvasPushContext context)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {context.APIKeyToken}");

            var response = await client.GetAsync($"{API_BASE_URL}/projects/{context.ProjectId}/assets?branchId={context.BranchID}&limit=1000");
            response.EnsureSuccessStatusCode();

            var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var assets = responseJson.RootElement.GetProperty("result").EnumerateArray().ToList();

            return assets;
        }

        private static async Task DownloadAsset(JsonElement asset, PlayCanvasPushContext context)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {context.APIKeyToken}");

            var assetId = asset.GetProperty("id").GetInt32();
            string? filename = asset.GetProperty("file").GetProperty("filename").GetString();

            var response = await client.GetAsync($"{API_BASE_URL}/assets/{assetId}/file/{filename}?branchId={context.BranchID}");
            response.EnsureSuccessStatusCode();

            string downloadDirectory = $"{Directory.GetParent(_outputFolder).ToString()}\\scripts";

            if (!Directory.Exists(downloadDirectory))
            {
                Directory.CreateDirectory(downloadDirectory);
            }

            await using var fileStream = new FileStream($"{downloadDirectory}\\{filename}", FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);

            OnStatusUpdated?.Invoke($"Downloaded: {filename}", LogType.Info);
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
            OnStatusUpdated?.Invoke("Pushing project to GitHub...", LogType.Info);

            if (projectFolder == null)
            {
                throw new Exception("Project folder not set.");
            }

            // Navigate to the project folder
            Environment.CurrentDirectory = projectFolder;

            OnStatusUpdated?.Invoke($"Current directory: {Environment.CurrentDirectory}", LogType.Info);

            try
            {
                RunGitCommand("add .");
                RunGitCommand("commit -m \"Automated sync from PlayCanvas\"");
                RunGitCommand("push -u origin master --force");
                Console.WriteLine("Project successfully pushed to GitHub!");
                OnStatusUpdated?.Invoke("Project successfully pushed to GitHub!", LogType.Success);
            }
            catch (Exception ex)
            {
                OnStatusUpdated?.Invoke($"An error occurred during Git operations: {ex.Message}", LogType.Success);
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
            _log = $"{_log}\n{line}";
            Console.WriteLine(line);
        }

        public static void CancelJob()
        {
            _cancellationTokenSource.Cancel();
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