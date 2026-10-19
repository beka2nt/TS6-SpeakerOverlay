using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace TS6_SpeakerOverlay
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer? _topmostTimer;
        private readonly DispatcherTimer _dragInputTimer;
        private readonly OverlayPlacementController _placement;
        private readonly MainViewModel _viewModel;
        private readonly bool _enableShellIntegration;
        private TrayIconHelper? _trayIcon;
        private bool _isDragging;

        public MainWindow() : this(new MainViewModel(), true) { }

        internal MainWindow(MainViewModel viewModel, bool enableShellIntegration = false)
        {
            InitializeComponent();
            DataContext = _viewModel = viewModel;
            _enableShellIntegration = enableShellIntegration;
            _placement = new OverlayPlacementController(this, viewModel.Config);
            // Sample while captured even if the pointer is stationary or another
            // app owns keyboard focus. A Shift release must rebase immediately.
            _dragInputTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
            _dragInputTimer.Tick += (_, _) => UpdateDrag();
            viewModel.PositionPresetRequested += _placement.ApplyPreset;
            if (enableShellIntegration)
                _trayIcon = new TrayIconHelper(this, GetIsLocked, Lock, Unlock, OpenSettings, RefreshData,
                    CheckForUpdates, trayIcon => _trayIcon = trayIcon);

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            KeyDown += MainWindow_KeyDown;
            MouseDown += MainWindow_MouseDown;
            MouseMove += MainWindow_MouseMove;
            MouseUp += MainWindow_MouseUp;
            LostMouseCapture += (_, _) => EndDrag();
            if (enableShellIntegration)
            {
                _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _topmostTimer.Tick += (_, _) => WindowHelper.ForceTopMost(this);
                _topmostTimer.Start();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Left = _viewModel.Config.WindowLeft;
            Top = _viewModel.Config.WindowTop;
            _placement.EnsureVisible();
            if (_viewModel.Config.PositionPreset is OverlayPosition preset) _placement.ApplyPreset(preset);
            if (_viewModel.IsOverlayLocked) WindowHelper.EnableClickThrough(this);
            _trayIcon?.UpdateTrayIcon();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_enableShellIntegration)
            {
                SaveWindowPosition();
                _viewModel.SaveConfig();
            }
            if (_trayIcon == null) return;
            e.Cancel = true;
            Hide();
            _trayIcon.UpdateTrayIcon();
        }

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || GetIsLocked()) return;
            _isDragging = _placement.BeginDrag();
            if (!_isDragging) return;
            if (!CaptureMouse()) EndDrag();
            else _dragInputTimer.Start();
        }

        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
            => UpdateDrag();

        private void UpdateDrag()
        {
            if (!_isDragging) return;
            if (GetIsLocked())
            {
                EndDrag();
                ReleaseMouseCapture();
                return;
            }
            _placement.Drag();
        }

        private void MainWindow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging || e.ChangedButton != MouseButton.Left) return;
            EndDrag();
            ReleaseMouseCapture();
        }

        private void EndDrag()
        {
            _isDragging = false;
            _dragInputTimer.Stop();
            _placement.EndDrag();
        }

        private void SaveWindowPosition()
        {
            _viewModel.Config.WindowLeft = Left;
            _viewModel.Config.WindowTop = Top;
        }

        private void RefreshData() => _viewModel.RefreshData();

        private void OpenSettings()
        {
            new Views.SettingsWindow(_viewModel).Show();
        }

        private void CheckForUpdates()
        {
            OpenSettings();
            _viewModel.CheckForUpdatesCommand.Execute(null);
        }

        public void ShowOverlay()
        {
            Show();
            Activate();
            _trayIcon?.UpdateTrayIcon();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.L || Keyboard.Modifiers != ModifierKeys.Control) return;
            ToggleLock();
            e.Handled = true;
        }

        private bool GetIsLocked() => _viewModel.IsOverlayLocked;

        private void Lock()
        {
            if (_viewModel.IsOverlayLocked) return;
            WindowHelper.EnableClickThrough(this);
            _viewModel.IsOverlayLocked = true;
            _trayIcon?.UpdateTrayIcon();
        }

        private void Unlock()
        {
            if (!_viewModel.IsOverlayLocked) return;
            WindowHelper.DisableClickThrough(this);
            _viewModel.IsOverlayLocked = false;
            _trayIcon?.UpdateTrayIcon();
        }

        private void ToggleLock()
        {
            if (GetIsLocked()) Unlock(); else Lock();
        }

        protected override void OnClosed(EventArgs e)
        {
            _topmostTimer?.Stop();
            _dragInputTimer.Stop();
            _viewModel.PositionPresetRequested -= _placement.ApplyPreset;
            _placement.Dispose();
            _trayIcon?.Dispose();
            _viewModel.Dispose();
            base.OnClosed(e);
        }
    }
}
