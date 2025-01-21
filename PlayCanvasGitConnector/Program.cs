using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DotNetEnv;

class Program
{
    private const string ApiBaseUrl = "https://playcanvas.com/api";
    private const string PlayCanvasBranchId = "6c77b015-f2c6-4f3c-ad68-9e7a9292cfb0";
    private const string AppName = "Playroom";
    private const string _environmentFile = "C:\\Users\\timde\\Documents\\GitHub\\New folder\\PlayCanvasGitConnector\\PlayCanvasGitConnector\\.env";

    private static string? _outputFolder;
    private static string? _gitFolder;
    private static string? _token;
    private static string? _projectId;
    private static string? _sceneId;

    static async Task Main()
    {
        Console.WriteLine($"{_environmentFile}");
        Env.Load(_environmentFile);

        _outputFolder = Environment.GetEnvironmentVariable("OUTPUT_FOLDER");
        _gitFolder = Environment.GetEnvironmentVariable("GIT_FOLDER");
        _token = Environment.GetEnvironmentVariable("PLAYCANVAS_TOKEN");
        _projectId = Environment.GetEnvironmentVariable("PLAYCANVAS_PROJECT_ID");
        _sceneId = Environment.GetEnvironmentVariable("SCENE_ID");

        try
        {

            // Step 1: Start the export job
            Console.WriteLine("Starting export job...");
            var jobId = await StartExportJob();

            // Step 2: Poll for job status
            Console.WriteLine("Waiting for export to complete...");
            var downloadUrl = await PollForJobCompletion(jobId);

            // Step 3: Download the exported app
            Console.WriteLine("Downloading exported app...");
            var zipFilePath = await DownloadApp(downloadUrl);

            // Step 4: Extract the ZIP file
            Console.WriteLine("Extracting app...");
            ExtractZip(zipFilePath, _outputFolder);

            Console.WriteLine($"App successfully exported to '{_outputFolder}'!");

            Console.WriteLine("Pushing project to GitHub...");
            PushToGitHub(_gitFolder);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private static async Task<int> StartExportJob()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");

        var requestBody = new
        {
            project_id = _projectId,
            scenes = new[] { _sceneId },
            name = AppName,
            branch_id = PlayCanvasBranchId
        };

        var response = await client.PostAsync($"{ApiBaseUrl}/apps/download",
            new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();

        var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Console.WriteLine($"{responseJson.RootElement.ToString()}");

        return responseJson.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<string> PollForJobCompletion(int jobId)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");

        while (true)
        {
            var response = await client.GetAsync($"{ApiBaseUrl}/jobs/{jobId}");
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

            if (status == "complete" && downloadUrl != null)
            {
                return downloadUrl;
            }
            else if (status == "error")
            {
                throw new Exception("Export job failed.");
            }

            await Task.Delay(2000); // Poll every 2 seconds
        }
    }

    private static async Task<string> DownloadApp(string downloadUrl)
    {
        using var client = new HttpClient();
        var zipFilePath = $"{AppName}.zip";

        var response = await client.GetAsync(downloadUrl);
        response.EnsureSuccessStatusCode();

        await using var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fileStream);

        return zipFilePath;
    }

    private static void ExtractZip(string zipFilePath, string outputFolder)
    {
        if (Directory.Exists(outputFolder))
        {
            Directory.Delete(outputFolder, true);
        }

        if (!Directory.Exists(_outputFolder))
        {
            Directory.CreateDirectory(_outputFolder);
        }

        ZipFile.ExtractToDirectory(zipFilePath, outputFolder);
    }

    private static void PushToGitHub(string? projectFolder)
    {
        if(projectFolder == null)
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

}
