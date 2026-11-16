using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TS6_SpeakerOverlay.Services;
using Brush = System.Windows.Media.Brush;

namespace TS6_SpeakerOverlay.Views
{
    public partial class LogViewerWindow : Window
    {
        private bool _errorsOnly;
        private readonly Func<bool, IReadOnlyList<string>> _readEntries;

        public LogViewerWindow() : this(errorsOnly => AppLogger.ReadRecentEntries(200, errorsOnly)) { }

        internal LogViewerWindow(Func<bool, IReadOnlyList<string>> readEntries)
        {
            _readEntries = readEntries;
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
            var entries = _readEntries(_errorsOnly);
            LogTextBox.Text = entries.Count == 0
                ? (TryFindResource("Lang_LogViewer_Empty") as string ?? "No matching log entries.")
                : string.Join(Environment.NewLine + Environment.NewLine, entries.Reverse());
            StatusText.Text = string.Format(
                TryFindResource("Lang_LogViewer_Count") as string ?? "Showing {0} recent entries",
                entries.Count);
            AllButton.Background = (Brush)FindResource(_errorsOnly ? "SurfaceRaisedBrush" : "SelectionBrush");
            ErrorsButton.Background = (Brush)FindResource(_errorsOnly ? "SelectionBrush" : "SurfaceRaisedBrush");
            AllButton.BorderBrush = (Brush)FindResource(_errorsOnly ? "BorderBrush" : "AccentBrush");
            ErrorsButton.BorderBrush = (Brush)FindResource(_errorsOnly ? "AccentBrush" : "BorderBrush");
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
