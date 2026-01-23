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
        private readonly Action _refreshAction; // [新增] 刷新回调
        private readonly Action<TrayIconHelper?> _setTrayIconRef;
        private bool _isExiting;

        // 菜单项引用
        private ToolStripMenuItem? _settingsMenuItem;
        private ToolStripMenuItem? _refreshMenuItem; // [新增]
        private ToolStripMenuItem? _showMenuItem;
        private ToolStripMenuItem? _hideMenuItem;
        private ToolStripMenuItem? _lockMenuItem;
        private ToolStripMenuItem? _unlockMenuItem;
        private ToolStripMenuItem? _exitMenuItem;

        // [修改] 构造函数增加 refreshAction
        public TrayIconHelper(Window mainWindow, Func<bool> getIsLocked, Action lockAction, Action unlockAction, Action openSettingsAction, Action refreshAction, Action<TrayIconHelper?> setTrayIconRef)
        {
            _mainWindow = mainWindow;
            _getIsLocked = getIsLocked;
            _lockAction = lockAction;
            _unlockAction = unlockAction;
            _openSettingsAction = openSettingsAction;
            _refreshAction = refreshAction;
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

            _settingsMenuItem = new ToolStripMenuItem("Settings");
            _settingsMenuItem.Click += (_, _) => _openSettingsAction.Invoke();

            // [新增] 刷新菜单
            _refreshMenuItem = new ToolStripMenuItem("Refresh");
            _refreshMenuItem.Click += (_, _) => _refreshAction.Invoke();

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

            contextMenu.Items.Add(_settingsMenuItem);
            contextMenu.Items.Add(_refreshMenuItem); // 加入菜单
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_showMenuItem);
            contextMenu.Items.Add(_hideMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_lockMenuItem);
            contextMenu.Items.Add(_unlockMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_exitMenuItem);

            contextMenu.Opening += (_, _) => UpdateMenuText();

            _notifyIcon.ContextMenuStrip = contextMenu;
            UpdateMenuText();
        }

        private void UpdateMenuText()
        {
            if (_settingsMenuItem == null) return;

            _settingsMenuItem.Text = LanguageHelper.GetString("Lang_Tray_Settings");
            // 刷新按钮的多语言 key，如果没加翻译就默认显示 Refresh
            _refreshMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Refresh"); 
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
            string statusKey;

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