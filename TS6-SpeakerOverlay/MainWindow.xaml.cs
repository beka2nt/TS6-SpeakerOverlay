using System.Windows;
using System.Windows.Input;
// 引用修正后的命名空间 (注意是下划线)
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TS6_SpeakerOverlay
{
    public partial class MainWindow : Window
    {
        private TrayIconHelper? _trayIcon;

        public MainWindow()
        {
            // 如果这里报错，请先无视，按照第三步操作"清理解决方案"即可修复
            InitializeComponent();

            // 监听窗口加载
            this.Loaded += MainWindow_Loaded;
            
            // 监听鼠标按下
            this.MouseDown += MainWindow_MouseDown;

            // 监听窗口关闭，防止直接退出
            this.Closing += MainWindow_Closing;

            // 监听键盘事件
            this.KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. 初始化位置 (例如左上角，或者屏幕中心)
            this.Left = 100;
            this.Top = 100;

            // 2. 重要：这里不再强制调用 EnableClickThrough
            // 我们让 ViewModel 的默认状态来决定是否穿透
            // 目前 ViewModel 默认为 false (不锁定)，所以这里什么都不用做

            // 3. 初始化托盘图标
            _trayIcon = new TrayIconHelper(
                this,
                GetIsLocked,
                Lock,
                Unlock,
                (trayIcon) => _trayIcon = trayIcon
            );
        }

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 只有按下左键，且 ViewModel 处于"未锁定"状态时，才允许拖动
            if (e.ChangedButton == MouseButton.Left)
            {
                // 获取当前的数据上下文
                if (DataContext is MainViewModel vm)
                {
                    // 如果没有锁定 (IsOverlayLocked == false)，允许拖拽
                    if (!vm.IsOverlayLocked)
                    {
                        // 这是一个系统方法，用于拖动无边框窗口
                        this.DragMove();
                    }
                }
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+L 切换锁定/解锁
            if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ToggleLock();
                e.Handled = true;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // If tray icon is null, application is shutting down
            if (_trayIcon == null)
            {
                return;
            }

            // 阻止窗口关闭，改为隐藏
            e.Cancel = true;
            this.Hide();
            _trayIcon.UpdateTrayIcon();
        }

        private bool GetIsLocked()
        {
            if (DataContext is MainViewModel vm)
            {
                return vm.IsOverlayLocked;
            }
            return false;
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
            if (GetIsLocked())
                Unlock();
            else
                Lock();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}