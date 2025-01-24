using System.Diagnostics;
using System.Windows;
using PlayCanvasGitConnector.LoggingServices;
using PlayCanvasGitConnector.Models;

namespace PlayCanvasGitConnector
{
    class Program
    {

        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private static PlayCanvasModel _playCanvasModel = new PlayCanvasModel();

        public static Action? OnProcessFinished { get; set; }

        internal static bool IsDevMode { get; set; } = false;

        private static MainViewModel _mainViewModel;

        [STAThread]
        static void Main(string[] args)
        {
            var app = new Application();
            MainWindow mainWindow = new MainWindow();
            app.Run(mainWindow);

            _mainViewModel = mainWindow.GetViewModel();

            LoggerService.Initialize();
            DirectoriesManager.Initialize();
        }

        public async static void StartSyncingProcess(PlayCanvasPushContext context)
        {
            if (!context.IsValid())
            {
                LoggerService.Log("Invalid context provided.", LogType.Error);
                context.LogContext(LogType.Error);
                await Task.Delay(500);
                OnProcessFinished?.Invoke();
                return;
            }

            try
            {
                // cancel the job if requested
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                // export the app
                await _playCanvasModel.ExportAppFile(context, _cancellationTokenSource);

                // download all scripts
                await _playCanvasModel.DownloadAllScripts(context, _cancellationTokenSource);

                // push the project to GitHub
                GitHubModel.PushToGitHub(DirectoriesManager.ProjectFolder);

                LoggerService.Log("Job finished successfully!", LogType.Success);
            }
            catch (Exception ex)
            {
                LoggerService.Log($"An error occurred: {ex.Message}", LogType.Error);

                if (IsDevMode)
                {
                    LoggerService.Log($"Stack Trace: {ex.StackTrace}", LogType.Error);
                    LoggerService.Log($"Source: {ex.Source}", LogType.Error);
                }
            }

            OnProcessFinished?.Invoke();
        }

        private bool IsOperationValid()
        {
            if (!DirectoriesManager.HasValidDirectories())
            {
                LoggerService.Log("Invalid directories.", LogType.Error);
                return false;
            }

            return true;
        }

        #region Utilities
        public static void CancelJob()
        {
            _cancellationTokenSource.Cancel();
        }
        #endregion

        #region Event Handling
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
}