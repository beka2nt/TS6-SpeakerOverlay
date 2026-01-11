using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace TS6_SpeakerOverlay.Helpers
{
    public class TrayIconHelper : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private readonly Window _mainWindow;
        private readonly Func<bool> _getIsLocked;
        private readonly Action _lockAction;
        private readonly Action _unlockAction;
        private readonly Action _openSettingsAction;
        private readonly Action<TrayIconHelper?> _setTrayIconRef;
        private bool _isExiting;

        // 菜单项引用，用于后续更新文字
        private ToolStripMenuItem? _settingsMenuItem;
        private ToolStripMenuItem? _showMenuItem;
        private ToolStripMenuItem? _hideMenuItem;
        private ToolStripMenuItem? _lockMenuItem;
        private ToolStripMenuItem? _unlockMenuItem;
        private ToolStripMenuItem? _exitMenuItem;

        public TrayIconHelper(Window mainWindow, Func<bool> getIsLocked, Action lockAction, Action unlockAction, Action openSettingsAction, Action<TrayIconHelper?> setTrayIconRef)
        {
            _mainWindow = mainWindow;
            _getIsLocked = getIsLocked;
            _lockAction = lockAction;
            _unlockAction = unlockAction;
            _openSettingsAction = openSettingsAction;
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
                if (e is MouseEventArgs { Button: MouseButtons.Left })
                {
                    if (_getIsLocked()) _unlockAction.Invoke();
                    else _lockAction.Invoke();
                    UpdateTrayIcon();
                }
            };

            var contextMenu = new ContextMenuStrip();

            // 初始化菜单项
            _settingsMenuItem = new ToolStripMenuItem("Settings");
            _settingsMenuItem.Click += (_, _) => _openSettingsAction.Invoke();

            _showMenuItem = new ToolStripMenuItem("Show");
            _showMenuItem.Click += (_, _) => { ShowWindow(); UpdateTrayIcon(); };

            _hideMenuItem = new ToolStripMenuItem("Hide");
            _hideMenuItem.Click += (_, _) => { HideWindow(); UpdateTrayIcon(); };

            _lockMenuItem = new ToolStripMenuItem("Lock");
            _lockMenuItem.Click += (_, _) => { _lockAction.Invoke(); UpdateTrayIcon(); };

            _unlockMenuItem = new ToolStripMenuItem("Unlock");
            _unlockMenuItem.Click += (_, _) => { _unlockAction.Invoke(); UpdateTrayIcon(); };

            _exitMenuItem = new ToolStripMenuItem("Exit");
            _exitMenuItem.Click += (_, _) => ExitApplication();

            // 组装菜单
            contextMenu.Items.Add(_settingsMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_showMenuItem);
            contextMenu.Items.Add(_hideMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_lockMenuItem);
            contextMenu.Items.Add(_unlockMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_exitMenuItem);

            // [关键] 每次打开菜单时，刷新文字（以防语言变了）
            contextMenu.Opening += (_, _) => UpdateMenuText();

            _notifyIcon.ContextMenuStrip = contextMenu;
            
            // 初次加载文字
            UpdateMenuText();
        }

        // [新增] 从资源字典读取最新语言并更新菜单
        private void UpdateMenuText()
        {
            if (_settingsMenuItem == null) return;

            _settingsMenuItem.Text = LanguageHelper.GetString("Lang_Tray_Settings");
            _showMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Show");
            _hideMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Hide");
            _lockMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Lock");
            _unlockMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Unlock");
            _exitMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Exit");
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
            string statusKey; // 状态提示也支持多语言

            if (!isVisible)
            {
                iconColor = Color.Gray;
                statusKey = " (Hidden)"; 
            }
            else if (isLocked)
            {
                iconColor = Color.FromArgb(79, 205, 142);
                statusKey = " (Locked)";
            }
            else
            {
                iconColor = Color.DodgerBlue;
                statusKey = " (Unlocked)";
            }

            _notifyIcon.Text = "TS6 Speaker Overlay" + statusKey;

            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = CreateIcon(iconColor);
            oldIcon?.Dispose();

            if (_showMenuItem != null) _showMenuItem.Enabled = !isVisible;
            if (_hideMenuItem != null) _hideMenuItem.Enabled = isVisible;
            if (_lockMenuItem != null) _lockMenuItem.Enabled = !isLocked;
            if (_unlockMenuItem != null) _unlockMenuItem.Enabled = isLocked;
            
            // 每次状态改变也顺便刷一下文字（双保险）
            UpdateMenuText();
        }

        private void ShowWindow() { _mainWindow.Show(); _mainWindow.Activate(); }
        private void HideWindow() => _mainWindow.Hide();

        private void ExitApplication()
        {
            _setTrayIconRef(null);
            Dispose();
            Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (_isExiting) return;
            _isExiting = true;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}