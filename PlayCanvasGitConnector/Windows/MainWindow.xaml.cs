using PlayCanvasGitConnector.LoggingServices;
using PlayCanvasGitConnector.Models;
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
        private Program MainProgram = new Program();
        private SolidColorBrush _infoColorBrush;
        private SolidColorBrush _successColorBrush;
        private SolidColorBrush _errorColorBrush;
        private SolidColorBrush _warningColorBrush;
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            LoggerService.RegisterForLog(DisplayStatusOnLabel);
            MainProgram.RegisterForProcessCompleted(OnProcessCompleted);
            //LogTextBlock.Text = "";

            // temporary color
            _successColorBrush = new SolidColorBrush()
            {
                Color = new Color() { R = 0, G = 178, B = 0, A = 255 }
            };

            _errorColorBrush = (SolidColorBrush)FindResource("PlayCanvasErrorRed");
            _infoColorBrush = (SolidColorBrush)FindResource("PlayCanvasWhite");
            _warningColorBrush = (SolidColorBrush)FindResource("PlayCanvasHighlightOrange");

            _viewModel = new MainViewModel(MainProgram);
            DataContext = _viewModel;
        }

        internal MainViewModel GetViewModel()
        {
            return _viewModel;
        }

        // TODO: must be moved to a view model of its own. Issue is that we can't bind the "InLInes" property of the TextBlock, so a long workaround is needed
        private void DisplayStatusOnLabel(string status, LogType logType = LogType.Info)
        {
            status = $"> {status}";

            // Determine the color based on the log type
            SolidColorBrush brush = logType switch
            {
                LogType.Info => _infoColorBrush,
                LogType.Success => _successColorBrush,
                LogType.Error => _errorColorBrush,
                LogType.Warning => _warningColorBrush,
            };

            // Create a new Run with the status text and color
            Run run = new Run($"{status}\n")
            {
                Foreground = brush
            };

            // in case the log was cleared or on start
            if (LogTextBlock.Inlines.Count > 0)
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
            DisplayStatusOnLabel($"Process completed", LogType.Info);
        }

        #region Clear Button
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBlock.Inlines.Clear();
        }
        #endregion

        #region Checkbox
        private void DevDebugCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Program.IsDevMode = true;
        }

        private void DevDebugCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            Program.IsDevMode = false;
        }
        #endregion
    }
}
