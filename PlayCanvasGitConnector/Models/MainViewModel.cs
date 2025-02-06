using Microsoft.WindowsAPICodePack.Dialogs;
using PlayCanvasGitConnector.LoggingServices;
using PlayCanvasGitConnector.Services;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace PlayCanvasGitConnector.Models
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _apiKeyToken = string.Empty;
        private string _projectId = string.Empty;
        private string _branchId = string.Empty;
        private string _scenesIds = string.Empty;

        private string _directoryPath = string.Empty;
        private string _remoteGitUrl = string.Empty;

        private bool _isSyncButtonEnabled = true;
        private bool _isStopButtonEnabled = false;
        private bool _isGitDirectory = true;

        public string APIKeyToken
        {
            get => _apiKeyToken;
            set
            {
                _apiKeyToken = value;
                OnPropertyChanged(nameof(APIKeyToken));
            }
        }
        public string ProjectId
        {
            get => _projectId;
            set
            {
                _projectId = value;
                OnPropertyChanged(nameof(ProjectId));
            }
        }
        public string BranchId
        {
            get => _branchId;
            set
            {
                _branchId = value;
                OnPropertyChanged(nameof(BranchId));
            }
        }
        public string ScenesIds
        {
            get => _scenesIds;
            set
            {
                _scenesIds = value;
                OnPropertyChanged(nameof(ScenesIds));
            }
        }
        public string DirectoryPath
        {
            get => _directoryPath;
            set
            {
                DirectoriesManager.SetOutputFolder(value);
                _directoryPath = value;
                OnPropertyChanged(nameof(DirectoryPath));
            }
        }
        public string RemoteGitUrl
        {
            get => _remoteGitUrl;
            set
            {
                _remoteGitUrl = value;
                OnPropertyChanged(nameof(RemoteGitUrl));
            }
        }

        public bool IsSyncButtonEnabled
        {
            get => _isSyncButtonEnabled;
            set
            {
                _isSyncButtonEnabled = value;
                OnPropertyChanged(nameof(IsSyncButtonEnabled));
            }
        }
        public bool IsStopButtonEnabled
        {
            get => _isStopButtonEnabled;
            set
            {
                _isStopButtonEnabled = value;
                OnPropertyChanged(nameof(IsStopButtonEnabled));
            }
        }
        public bool IsGitDirectory
        {
            get => _isGitDirectory;
            set
            {
                _isGitDirectory = value;
                OnPropertyChanged(nameof(IsGitDirectory));
            }
        }

        public ICommand SyncCommand { get; }
        public ICommand AutoFillCommand { get; }
        public ICommand CacheAutoFillCommand { get; }
        public ICommand StopJobCommand { get; }
        public ICommand BrowseCommand { get; }

        public MainViewModel(Program mainProgram)
        {
            SyncCommand = new RelayCommand(OnSync);
            AutoFillCommand = new RelayCommand(OnAutoFill);
            CacheAutoFillCommand = new RelayCommand(CacheAutoFillData);
            StopJobCommand = new RelayCommand(OnStopJob);
            BrowseCommand = new RelayCommand(OnBrowse);

            IsSyncButtonEnabled = true;
            IsStopButtonEnabled = false;

            mainProgram.RegisterForProcessCompleted(OnProcessFinished);
        }

        private void OnBrowse(object parameter)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select git repository"
            };

            string directory = string.Empty;

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                directory = dialog.FileName;
            }

            string gitDirectory = Path.Combine(directory, ".git");

            if (!Directory.Exists(gitDirectory))
            {
                RemoteGitUrl = string.Empty;
                IsGitDirectory = false;
                LoggerService.Log($"{gitDirectory} doesn't contain .git. Please enter a remote git repository URL.", LogType.Error);
            }
            else
            {
                IsGitDirectory = true;
            }

            DirectoryPath = directory;

            try
            {
                RemoteGitUrl = GitHubModel.GetRemoteRepository(DirectoryPath);
            }
            catch
            {
                LoggerService.Log($"No remote git repository is associated with local directory: {DirectoryPath}", LogType.Warning);
            }    
        }

        private void OnStopJob(object parameter)
        {
            Program.CancelJob();
        }

        private void OnSync(object parameter)
        {
            PlayCanvasPushContext context;

            context = new PlayCanvasPushContext
            {
                APIKeyToken = APIKeyToken,
                ProjectId = ProjectId,
                BranchID = BranchId,
                SceneIDs = ScenesIds.Split(','),
                FileDirectory = DirectoryPath,
                RemoteGitURL = RemoteGitUrl
            };

            string validityReport = PlayCanvasPushContextValidator.Validate(context);

            if (!string.IsNullOrEmpty(validityReport))
            {
                LoggerService.Log(validityReport, LogType.Error);
                return;
            }

            IsSyncButtonEnabled = false;
            IsStopButtonEnabled = true;

            Program.StartSyncingProcess(context);
        }

        private void OnAutoFill(object parameter)
        {
            string cacheFile = FileServices.ReadCacheFile(DirectoriesManager.AppFolder);

            if (string.IsNullOrEmpty(cacheFile))
            {
                LoggerService.Log("No cache file found", LogType.Error);
                return;
            }

            try
            {
                PlayCanvasPushContext context = JsonSerializer.Deserialize<PlayCanvasPushContext>(cacheFile);

                APIKeyToken = context.APIKeyToken;
                ProjectId = context.ProjectId;
                BranchId = context.BranchID;
                ScenesIds = string.Join(",", context.SceneIDs);
                DirectoryPath = context.FileDirectory;
                RemoteGitUrl = GitHubModel.GetRemoteRepository(DirectoryPath);
            }
            catch (Exception ex)
            {
                LoggerService.Log($"{ex.Message}", LogType.Error);
            }
        }

        private void CacheAutoFillData(object parameter)
        {
            string cacheFile = FileServices.ReadCacheFile(DirectoriesManager.AppFolder);

            if (cacheFile != string.Empty)
            {
                var result = MessageBox.Show(
                    "Cache file already exists. Do you want to override it? (This action cannot be reversed)", 
                    "Cache exists", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            var context = new PlayCanvasPushContext
            {
                APIKeyToken = APIKeyToken,
                ProjectId = ProjectId,
                BranchID = BranchId,
                SceneIDs = ScenesIds.Split(','),
                FileDirectory = DirectoryPath,
                RemoteGitURL = GitHubModel.GetRemoteRepository(DirectoryPath)
            };

            string jsonString = JsonSerializer.Serialize(context, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            string cacheFileData = $"{context.APIKeyToken}.{context.ProjectId}.{context.BranchID}.{string.Join(",", context.SceneIDs)}";

            if (cacheFileData == "...")
            {
                LoggerService.Log("Cache data is empty", LogType.Error);
                cacheFileData = string.Empty;
            }

            FileServices.WriteCacheFile(DirectoriesManager.AppFolder, jsonString);
            LoggerService.Log(DirectoriesManager.AppFolder, LogType.Info);
            //LoggerService.Log($"Cache file written to \"{Path.GetFullPath(DirectoriesManager.AppFolder)}\"\nData:", LogType.Info);
            context.LogContext();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnProcessFinished()
        {
            IsSyncButtonEnabled = true;
            IsStopButtonEnabled = false;
        }
    }
}
