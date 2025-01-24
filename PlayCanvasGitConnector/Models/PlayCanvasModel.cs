using System.IO.Compression;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using PlayCanvasGitConnector.LoggingServices;

namespace PlayCanvasGitConnector.Models
{
    internal class PlayCanvasModel
    {
        private const string PLAYCANVAS_TOKEN = "PLAYCANVAS_TOKEN";
        private const string PLAYCANVAS_PROJECT_ID = "PLAYCANVAS_PROJECT_ID";
        private const string SCENE_ID = "SCENE_ID";
        private const string API_BASE_URL = "https://playcanvas.com/api";
        private const string APP_NAME = "Playroom";


        internal async Task<string> ExportAppFile(PlayCanvasPushContext context, CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();

                LoggerService.Log("Starting export job...", LogType.Info);
                var jobId = await StartExportJob(context);

                // poll for job status
                LoggerService.Log("Waiting for export to complete...", LogType.Info);
                var downloadUrl = await PollForJobCompletion(jobId, context, cancellationTokenSource);

                // download the exported app
                LoggerService.Log("Downloading exported app...", LogType.Info);

                string zipFilePath = await DownloadApp(downloadUrl);

                // extract the ZIP file and delete it
                LoggerService.Log("Extracting app...", LogType.Info);
                ExtractZip(zipFilePath);

                LoggerService.Log($"App successfully exported to '{DirectoriesManager.OutputFolder}'!", LogType.Success);
            }
            catch (OperationCanceledException)
            {
                LoggerService.Log("Job cancelled.", LogType.Success);
            }
            catch (Exception ex)
            {
                LoggerService.Log($"An error occurred: {ex.Message}", LogType.Success);
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
                branch_id = context.BranchID
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

        private static async Task<string> PollForJobCompletion(int jobId, PlayCanvasPushContext context, CancellationTokenSource cancellationTokenSource)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {context.APIKeyToken}");

            while (true)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();

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

        internal async Task DownloadAllScripts(PlayCanvasPushContext context, CancellationTokenSource cancellationTokenSource)
        {
            LoggerService.Log("Downloading scripts...", LogType.Info);

            var assets = await GetAllAssets(context);

            // filter the scripts
            var scripts = assets.Where(asset => asset.GetProperty("type").GetString() == "script");

            foreach (var script in scripts)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                await DownloadAsset(script, context);
            }

            LoggerService.Log("Scripts downloaded successfully!", LogType.Success);
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

            string downloadDirectory = $"{DirectoriesManager.ProjectFolder}\\scripts";

            if (!Directory.Exists(downloadDirectory))
            {
                Directory.CreateDirectory(downloadDirectory);
            }

            await using var fileStream = new FileStream($"{downloadDirectory}\\{filename}", FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);

            LoggerService.Log($"Downloaded: {filename} at {downloadDirectory}", LogType.Info);
        }

        private static void ExtractZip(string zipFilePath)
        {
            string outputFolder = DirectoriesManager.OutputFolder;

            if (outputFolder == null)
            {
                throw new Exception("Output folder directory not set.");
            }

            if (Directory.Exists(outputFolder))
            {
                Directory.Delete(outputFolder, true);
            }

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            ZipFile.ExtractToDirectory(zipFilePath, outputFolder);
            LoggerService.Log($"Extracted to: {outputFolder}", LogType.Success);
        }
    }
}
