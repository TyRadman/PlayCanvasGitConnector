using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PlayCanvasGitConnector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Program Program = new Program();
        private SolidColorBrush _infoColorBrush;
        private SolidColorBrush _successColorBrush;
        private SolidColorBrush _errorColorBrush;

        public MainWindow()
        {
            InitializeComponent();

            Program.RegisterForLog(DisplayStatusOnLabel);
            Program.RegisterForProcessCompleted(OnProcessCompleted);
            LogTextBlock.Text = "";
            StopButton.IsEnabled = false;

            _successColorBrush = (SolidColorBrush)FindResource("PlayCanvasHighlightOrange");
            _errorColorBrush = (SolidColorBrush)FindResource("PlayCanvasErrorRed");
            _infoColorBrush = (SolidColorBrush)FindResource("PlayCanvasWhite");
        }

        private void DisplayStatusOnLabel(string status, LogType logType = LogType.Info)
        {
            status = $"> {status}";

            // Determine the color based on the log type
            SolidColorBrush brush = logType switch
            {
                LogType.Info => _infoColorBrush,
                LogType.Success => _successColorBrush,
                LogType.Error => _errorColorBrush
            };

            // Create a new Run with the status text and color
            Run run = new Run($"{status}\n")
            {
                Foreground = brush
            };

            // in case the log was cleared or on start
            if(LogTextBlock.Inlines.Count > 0)
            {
                LogTextBlock.Inlines.InsertAfter(LogTextBlock.Inlines.LastInline, run);
            }
            else
            {
                LogTextBlock.Inlines.Add(run);
            }

            // temp
            int maxLines = 50;

            if (LogTextBlock.Inlines.Count > maxLines)
            {
                var inlines = LogTextBlock.Inlines.ToList();

                for (int i = 0; i < inlines.Count - maxLines; i++)
                {
                    LogTextBlock.Inlines.Remove(inlines[i]);
                }
            }

            LogScrollViewer.ScrollToEnd();
        }

        private void OnProcessCompleted()
        {
            PushButton.IsEnabled = true;

            StopButton.IsEnabled = false;
            DisplayStatusOnLabel($"Process completed", LogType.Info);
        }

        #region Push Button
        private void PushButton_Click(object sender, RoutedEventArgs e)
        {
            PlayCanvasPushContext context;

            bool useEnvVariables = UseEnvVariablesCheckBox.IsChecked.Value;

            if (useEnvVariables)
            {
                context = Program.GetEnvContext();
            }
            else
            {
                context = new PlayCanvasPushContext
                {
                    APIKeyToken = ApiTokenTextBox.Text,
                    ProjectId = ProjectIDTextBox.Text,
                    BranchID = BranchIDTextBox.Text,
                    SceneIDs = SceneIDTextBox.Text.Split(',')
                };
            }

            Program.StartPushingProcess(context);
            PushButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }
        #endregion

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {

        }

        #region Clear Button
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBlock.Text = string.Empty;
        }
        #endregion

        #region Checkbox
        private void UseEnvVariablesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ApiTokenTextBox.IsEnabled = false;
            ProjectIDTextBox.IsEnabled = false;
            BranchIDTextBox.IsEnabled = false;
            SceneIDTextBox.IsEnabled = false;
        }

        private void UseEnvVariablesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ApiTokenTextBox.IsEnabled = true;
            ProjectIDTextBox.IsEnabled = true;
            BranchIDTextBox.IsEnabled = true;
            SceneIDTextBox.IsEnabled = true;
        }
        #endregion
    }
}
