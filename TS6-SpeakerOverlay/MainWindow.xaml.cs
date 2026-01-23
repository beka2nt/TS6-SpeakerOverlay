using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.ViewModels;

// 消除歧义
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace TS6_SpeakerOverlay
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _topmostTimer;
        private TrayIconHelper? _trayIcon; 
        
        // 拖拽变量
        private bool _isDragging = false;
        private Point _lastMousePosition; // 上一次鼠标相对于窗口的位置

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            this.KeyDown += MainWindow_KeyDown; 
            
            // 绑定鼠标事件
            this.MouseDown += MainWindow_MouseDown;
            this.MouseMove += MainWindow_MouseMove;
            this.MouseUp += MainWindow_MouseUp;

            _topmostTimer = new DispatcherTimer();
            _topmostTimer.Interval = TimeSpan.FromSeconds(2); 
            _topmostTimer.Tick += (s, e) => WindowHelper.ForceTopMost(this);
            _topmostTimer.Start();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _trayIcon = new TrayIconHelper(
                this, GetIsLocked, Lock, Unlock, OpenSettings, RefreshData,
                (trayIcon) => _trayIcon = trayIcon
            );

            // 手动加载位置
            if (DataContext is MainViewModel vm)
            {
                this.Left = vm.Config.WindowLeft;
                this.Top = vm.Config.WindowTop;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowPosition();
            if (_trayIcon == null) return;
            e.Cancel = true;
            this.Hide();
            _trayIcon.UpdateTrayIcon();
        }

        // --- [核心修改] 相对增量拖拽逻辑 (解决瞬移问题) ---

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !GetIsLocked())
            {
                _isDragging = true;
                // 记录按下时，鼠标相对于窗口内部的坐标
                _lastMousePosition = e.GetPosition(this);
                this.CaptureMouse(); // 捕获鼠标，防止拖出窗口外丢失
            }
        }
        private void RefreshData()
        {
            if (DataContext is MainViewModel vm)
            {
                vm.RefreshData();
            }
        }
        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                // 获取当前鼠标相对于窗口的坐标
                Point currentMousePosition = e.GetPosition(this);

                // 计算位移量 (当前 - 上次)
                // 这里的单位都是 WPF 逻辑单位，不会受 DPI 影响
                double deltaX = currentMousePosition.X - _lastMousePosition.X;
                double deltaY = currentMousePosition.Y - _lastMousePosition.Y;

                // 应用位移到窗口位置
                this.Left += deltaX;
                this.Top += deltaY;

                // 注意：这里不需要更新 _lastMousePosition
                // 因为随着窗口移动，鼠标相对于窗口的逻辑位置理论上应该保持不变
                // 任何微小的偏差直接累加到 Position 上即可
            }
        }

        private void MainWindow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.ReleaseMouseCapture();
                SaveWindowPosition(); // 拖拽结束保存
            }
        }

        // ----------------------------------------------------

        private void SaveWindowPosition()
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Config.WindowLeft = this.Left;
                vm.Config.WindowTop = this.Top;
            }
        }

        private void OpenSettings()
        {
            if (DataContext is MainViewModel vm)
            {
                var settingsWindow = new Views.SettingsWindow(vm);
                settingsWindow.Show();
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ToggleLock();
                e.Handled = true;
            }
        }

        private bool GetIsLocked()
        {
            return (DataContext is MainViewModel vm) && vm.IsOverlayLocked;
        }

        private void Lock()
        {
            if (DataContext is MainViewModel vm && !vm.IsOverlayLocked)
            {
                WindowHelper.EnableClickThrough(this);
                vm.IsOverlayLocked = true;
                _trayIcon?.UpdateTrayIcon();
            }
        }

        private void Unlock()
        {
            if (DataContext is MainViewModel vm && vm.IsOverlayLocked)
            {
                WindowHelper.DisableClickThrough(this);
                vm.IsOverlayLocked = false;
                _trayIcon?.UpdateTrayIcon();
            }
        }

        private void ToggleLock()
        {
            if (GetIsLocked()) Unlock(); else Lock();
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}