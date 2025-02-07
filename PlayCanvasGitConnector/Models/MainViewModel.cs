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
        public class DataTextBox : INotifyPropertyChanged
        {
            private const string WARNING_ICON_PATH = "pack://application:,,,/Resources/Warning.png";
            private const string CHECK_MARK_ICON_PATH = "pack://application:,,,/Resources/Check.png";
            private const string LIGHT_BULB_ICON_PATH = "pack://application:,,,/Resources/LightBulb.png";

            public enum State
            {
                Warning,
                Check, 
                Tip,
                None
            }

            public State TextBoxState { get; set; } = State.None;
            private bool _isRequired = true;

            private string _tooltipMessage = string.Empty;

            public event PropertyChangedEventHandler? PropertyChanged;

            public DataTextBox(bool isRequired = true, string tooltip = "")
            {
                _isRequired = isRequired;
                _tooltipMessage = tooltip;
                Tooltip = _tooltipMessage;

                if (_isRequired)
                {
                    IconSource = WARNING_ICON_PATH;
                }
                else
                {
                    IconSource = LIGHT_BULB_ICON_PATH;
                }
            }

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            private string _text = string.Empty;
            public string Text
            {
                get => _text;
                set
                {
                    _text = value;
                    OnPropertyChanged(nameof(Text));
                    OnTextChanged(value);
                }
            }

            private void OnTextChanged(string text)
            {
                if (_text.Length > 0)
                {
                    TextBoxState = State.Check;
                    IconSource = CHECK_MARK_ICON_PATH;
                }
                else
                {
                    if (_isRequired)
                    {
                        TextBoxState = State.Warning;
                        IconSource = WARNING_ICON_PATH;
                    }
                    else
                    {
                        TextBoxState = State.Tip;
                        IconSource = LIGHT_BULB_ICON_PATH;
                    }
                }
            }

            private Visibility _iconVisibility = Visibility.Hidden;
            public Visibility IconVisibility
            {
                get => _iconVisibility;
                set
                {
                    _iconVisibility = value;
                    OnPropertyChanged(nameof(IconVisibility));
                }
            }

            private string _iconSource;

            public string IconSource
            {
                get => _iconSource;
                set
                {
                    _iconSource = value;
                    OnPropertyChanged(nameof(IconSource));
                }
            }

            private string _tooltip = string.Empty;

            public string Tooltip
            {
                get => _tooltip;
                set
                {
                    _tooltip = value;
                    OnPropertyChanged(nameof(Tooltip));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public DataTextBox APIKeyTokenTextBox { get; }
        public DataTextBox ProjectIDTextBox { get; }
        public DataTextBox BranchIDTextBox { get; }
        public DataTextBox SceneIDsTextBox { get; }
        public DataTextBox DirectoryPathTextBox { get; }
        public DataTextBox GitRemoteURLTextBox { get; }

        private bool _isSyncButtonEnabled = true;
        public bool IsSyncButtonEnabled
        {
            get => _isSyncButtonEnabled;
            set
            {
                _isSyncButtonEnabled = value;
                OnPropertyChanged(nameof(IsSyncButtonEnabled));
            }
        }
        private bool _isStopButtonEnabled = false;
        public bool IsStopButtonEnabled
        {
            get => _isStopButtonEnabled;
            set
            {
                _isStopButtonEnabled = value;
                OnPropertyChanged(nameof(IsStopButtonEnabled));
            }
        }
        private bool _isGitDirectory = true;
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
            APIKeyTokenTextBox = new DataTextBox();
            ProjectIDTextBox = new DataTextBox();
            BranchIDTextBox = new DataTextBox(false, "The main branch will be set by default.");
            SceneIDsTextBox = new DataTextBox(false, "All the scenes will be set by default.");
            DirectoryPathTextBox = new DataTextBox();
            GitRemoteURLTextBox = new DataTextBox();

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
                GitRemoteURLTextBox.Text = string.Empty;
                IsGitDirectory = false;
                LoggerService.Log($"{gitDirectory} doesn't contain .git. Please enter a remote git repository URL.", LogType.Error);
            }
            else
            {
                IsGitDirectory = true;
            }

            DirectoryPathTextBox.Text = directory;

            try
            {
                GitRemoteURLTextBox.Text = GitHubModel.GetRemoteRepository(DirectoryPathTextBox.Text);
            }
            catch
            {
                LoggerService.Log($"No remote git repository is associated with local directory: {DirectoryPathTextBox.Text}", LogType.Warning);
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
                APIKeyToken = APIKeyTokenTextBox.Text,
                ProjectId = ProjectIDTextBox.Text,
                BranchID = BranchIDTextBox.Text,
                SceneIDs = SceneIDsTextBox.Text.Split(','),
                FileDirectory = DirectoryPathTextBox.Text,
                RemoteGitURL = GitRemoteURLTextBox.Text
            };

            UpdateTextBoxesBasedOnContext(context);

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

        private void UpdateTextBoxesBasedOnContext(PlayCanvasPushContext context)
        {
            if(string.IsNullOrEmpty(context.APIKeyToken))
            {
                APIKeyTokenTextBox.IconVisibility = Visibility.Visible;
            }
            else
            {
                APIKeyTokenTextBox.IconVisibility = Visibility.Hidden;
            }

            if (string.IsNullOrEmpty(context.ProjectId))
            {
                ProjectIDTextBox.IconVisibility = Visibility.Visible;
            }
            else
            {
                ProjectIDTextBox.IconVisibility = Visibility.Hidden;
            }

            if (string.IsNullOrEmpty(context.BranchID))
            {
                BranchIDTextBox.IconVisibility = Visibility.Visible;
            }
            else
            {
                BranchIDTextBox.IconVisibility = Visibility.Hidden;
            }

            if (context.SceneIDs == null || context.SceneIDs.Length == 0 || (context.SceneIDs.Length == 1 && string.IsNullOrEmpty(context.SceneIDs[0])))
            {
                SceneIDsTextBox.IconVisibility = Visibility.Visible;
            }
            else
            {
                SceneIDsTextBox.IconVisibility = Visibility.Hidden;
                LoggerService.Log($"Scene IDs: {context.SceneIDs[0]}", LogType.Info);
            }

            if (string.IsNullOrEmpty(context.FileDirectory))
            {
                DirectoryPathTextBox.IconVisibility = Visibility.Visible;
            }
            else
            {
                DirectoryPathTextBox.IconVisibility = Visibility.Hidden;
            }

            if (string.IsNullOrEmpty(context.RemoteGitURL))
            {
                GitRemoteURLTextBox.IconVisibility = Visibility.Visible;
            }
            else
            {
                GitRemoteURLTextBox.IconVisibility = Visibility.Hidden;
            }
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

                APIKeyTokenTextBox.Text = context.APIKeyToken;
                ProjectIDTextBox.Text = context.ProjectId;
                BranchIDTextBox.Text = context.BranchID;
                SceneIDsTextBox.Text = string.Join(",", context.SceneIDs);
                DirectoryPathTextBox.Text = context.FileDirectory;
                GitRemoteURLTextBox.Text = GitHubModel.GetRemoteRepository(DirectoryPathTextBox.Text);
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
                APIKeyToken = APIKeyTokenTextBox.Text,
                ProjectId = ProjectIDTextBox.Text,
                BranchID = BranchIDTextBox.Text,
                SceneIDs = SceneIDsTextBox.Text.Split(','),
                FileDirectory = DirectoryPathTextBox.Text,
                RemoteGitURL = GitHubModel.GetRemoteRepository(DirectoryPathTextBox.Text)
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
