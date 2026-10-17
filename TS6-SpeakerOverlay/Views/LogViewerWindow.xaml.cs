using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TS6_SpeakerOverlay.Services;

namespace TS6_SpeakerOverlay.Views
{
    public partial class LogViewerWindow : Window
    {
        private bool _errorsOnly;

        public LogViewerWindow()
        {
            InitializeComponent();
            RefreshEntries();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void All_Click(object sender, RoutedEventArgs e)
        {
            _errorsOnly = false;
            RefreshEntries();
        }

        private void Errors_Click(object sender, RoutedEventArgs e)
        {
            _errorsOnly = true;
            RefreshEntries();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshEntries();

        private void RefreshEntries()
        {
            var entries = AppLogger.ReadRecentEntries(200, _errorsOnly);
            LogTextBox.Text = entries.Count == 0
                ? (TryFindResource("Lang_LogViewer_Empty") as string ?? "No matching log entries.")
                : string.Join(Environment.NewLine + Environment.NewLine, entries.Reverse());
            StatusText.Text = string.Format(
                TryFindResource("Lang_LogViewer_Count") as string ?? "Showing {0} recent entries",
                entries.Count);
            AllButton.Background = new SolidColorBrush(_errorsOnly ? System.Windows.Media.Color.FromRgb(255, 255, 255) : System.Windows.Media.Color.FromRgb(220, 244, 236));
            ErrorsButton.Background = new SolidColorBrush(_errorsOnly ? System.Windows.Media.Color.FromRgb(220, 244, 236) : System.Windows.Media.Color.FromRgb(255, 255, 255));
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(LogTextBox.Text)) System.Windows.Clipboard.SetText(LogTextBox.Text);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = AppLogger.LogDirectoryPath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to open the logs directory from the log viewer.", ex);
                System.Windows.MessageBox.Show(TryFindResource("Lang_Log_OpenFailed") as string ?? "Unable to open the logs directory.");
            }
        }
    }
}
