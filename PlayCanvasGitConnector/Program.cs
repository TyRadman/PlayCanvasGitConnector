using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DotNetEnv;

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
    private static string? _token;
    private static string? _projectId;
    private static string? _sceneId;

    static async Task Main()
    {
        string? pathToEnv = $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName}\\.env";

        if (pathToEnv == null)
        {
            throw new Exception("Path to .env file not found.");
        }

        Env.Load(pathToEnv);

        _outputFolder = Environment.GetEnvironmentVariable(OUTPUT_FOLDER);
        _gitFolder = Environment.GetEnvironmentVariable(GIT_FOLDER);
        _token = Environment.GetEnvironmentVariable(PLAYCANVAS_TOKEN);
        _projectId = Environment.GetEnvironmentVariable(PLAYCANVAS_PROJECT_ID);
        _sceneId = Environment.GetEnvironmentVariable(SCENE_ID);

        try
        {
            // start the export job
            Console.WriteLine("Starting export job...");
            var jobId = await StartExportJob();

            // poll for job status
            Console.WriteLine("Waiting for export to complete...");
            var downloadUrl = await PollForJobCompletion(jobId);

            // download the exported app
            Console.WriteLine("Downloading exported app...");
            var zipFilePath = await DownloadApp(downloadUrl);

            // extract the ZIP file and delete it
            Console.WriteLine("Extracting app...");
            ExtractZip(zipFilePath);

            Console.WriteLine($"App successfully exported to '{_outputFolder}'!");

            // push the project to GitHub
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

    private static async Task<string> PollForJobCompletion(int jobId)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");

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
        if(_outputFolder == null)
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
