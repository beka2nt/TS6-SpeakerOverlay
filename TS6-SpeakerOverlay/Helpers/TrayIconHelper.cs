using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace TS6_SpeakerOverlay.Helpers
{
    public class TrayIconHelper : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private readonly Window _mainWindow;
        private readonly Func<bool> _getIsLocked;
        private readonly Action _lockAction;
        private readonly Action<TrayIconHelper?> _setTrayIconRef;
        private bool _isExiting;

        private ToolStripMenuItem? _showMenuItem;
        private ToolStripMenuItem? _hideMenuItem;
        private ToolStripMenuItem? _lockMenuItem;
        private ToolStripMenuItem? _unlockMenuItem;
        private readonly Action _unlockAction;

        public TrayIconHelper(Window mainWindow, Func<bool> getIsLocked, Action lockAction, Action unlockAction,
            Action<TrayIconHelper?> setTrayIconRef)
        {
            _mainWindow = mainWindow;
            _getIsLocked = getIsLocked;
            _lockAction = lockAction;
            _unlockAction = unlockAction;
            _setTrayIconRef = setTrayIconRef;
            InitializeTrayIcon();
            UpdateTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateIcon(Color.Gray),
                Visible = true,
                Text = "TS6 Speaker Overlay"
            };

            _notifyIcon.Click += (_, e) =>
            {
                var mouseEvent = e as MouseEventArgs;
                if (mouseEvent?.Button == MouseButtons.Left)
                {
                    if (_getIsLocked())
                        _unlockAction.Invoke();
                    else
                        _lockAction.Invoke();

                    UpdateTrayIcon();
                }
            };

            var contextMenu = new ContextMenuStrip();

            _showMenuItem = new ToolStripMenuItem("显示");
            _showMenuItem.Click += (_, _) =>
            {
                ShowWindow();
                UpdateTrayIcon();
            };

            _hideMenuItem = new ToolStripMenuItem("隐藏");
            _hideMenuItem.Click += (_, _) =>
            {
                HideWindow();
                UpdateTrayIcon();
            };

            _lockMenuItem = new ToolStripMenuItem("锁定");
            _lockMenuItem.Click += (_, _) =>
            {
                _lockAction.Invoke();
                UpdateTrayIcon();
            };

            _unlockMenuItem = new ToolStripMenuItem("解锁");
            _unlockMenuItem.Click += (_, _) =>
            {
                _unlockAction.Invoke();
                UpdateTrayIcon();
            };

            var exitMenuItem = new ToolStripMenuItem("退出程序");
            exitMenuItem.Click += (_, _) => ExitApplication();

            contextMenu.Items.Add(_showMenuItem);
            contextMenu.Items.Add(_hideMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_lockMenuItem);
            contextMenu.Items.Add(_unlockMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitMenuItem);

            contextMenu.Opening += (_, _) => UpdateTrayIcon();

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private Icon CreateIcon(Color color)
        {
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 2, 2, 12, 12);
                }
            }

            return Icon.FromHandle(bitmap.GetHicon());
        }

        public void UpdateTrayIcon()
        {
            if (_notifyIcon == null) return;

            var isVisible = _mainWindow.Visibility == Visibility.Visible;
            var isLocked = _getIsLocked();

            Color iconColor;
            if (!isVisible)
            {
                iconColor = Color.Gray;
                _notifyIcon.Text = "TS6 Speaker Overlay - 已隐藏";
            }
            else if (isLocked)
            {
                iconColor = Color.FromArgb(79, 205, 142);
                _notifyIcon.Text = "TS6 Speaker Overlay - 已锁定";
            }
            else
            {
                iconColor = Color.DodgerBlue;
                _notifyIcon.Text = "TS6 Speaker Overlay - 未锁定";
            }

            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = CreateIcon(iconColor);
            oldIcon?.Dispose();

            _showMenuItem?.Enabled = !isVisible;
            _hideMenuItem?.Enabled = isVisible;
            _lockMenuItem?.Enabled = !isLocked;
            _unlockMenuItem?.Enabled = isLocked;
        }

        private void ShowWindow()
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        }

        private void HideWindow()
        {
            _mainWindow.Hide();
        }

        private void ExitApplication()
        {
            _setTrayIconRef(null);
            Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (_isExiting) return;
            _isExiting = true;

            if (_notifyIcon != null)
            {
                _notifyIcon.Icon?.Dispose();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}