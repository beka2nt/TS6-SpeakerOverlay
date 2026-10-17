using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using TS6_SpeakerOverlay.Services;
using Application = System.Windows.Application;

namespace TS6_SpeakerOverlay.Helpers
{
    public class TrayIconHelper : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private readonly Window _mainWindow;
        private readonly Func<bool> _getIsLocked;
        private readonly Action _lockAction;
        private readonly Action _unlockAction;
        private readonly Action _openSettingsAction;
        private readonly Action _refreshAction; // [新增] 刷新回调
        private readonly Action _checkUpdatesAction;
        private readonly Action<TrayIconHelper?> _setTrayIconRef;
        private bool _isExiting;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        // 菜单项引用
        private ToolStripMenuItem? _statusLabel;
        private ToolStripMenuItem? _settingsMenuItem;
        private ToolStripMenuItem? _refreshMenuItem; // [新增]
        private ToolStripMenuItem? _updatesMenuItem;
        private ToolStripMenuItem? _languageMenuItem;
        private ToolStripMenuItem? _openLogsMenuItem;
        private ToolStripMenuItem? _showMenuItem;
        private ToolStripMenuItem? _hideMenuItem;
        private ToolStripMenuItem? _lockMenuItem;
        private ToolStripMenuItem? _unlockMenuItem;
        private ToolStripMenuItem? _exitMenuItem;

        // [修改] 构造函数增加 refreshAction
        public TrayIconHelper(Window mainWindow, Func<bool> getIsLocked, Action lockAction, Action unlockAction, Action openSettingsAction, Action refreshAction, Action checkUpdatesAction, Action<TrayIconHelper?> setTrayIconRef)
        {
            _mainWindow = mainWindow;
            _getIsLocked = getIsLocked;
            _lockAction = lockAction;
            _unlockAction = unlockAction;
            _openSettingsAction = openSettingsAction;
            _refreshAction = refreshAction;
            _checkUpdatesAction = checkUpdatesAction;
            _setTrayIconRef = setTrayIconRef;
            InitializeTrayIcon();
            UpdateTrayIcon();
            LanguageHelper.LanguageChanged += UpdateMenuText;
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

            _contextMenu = CreateMenuShell();

            _statusLabel = CreateMenuItem("TS6 Speaker Overlay", MenuGlyph.Status);
            _statusLabel.ForeColor = Color.FromArgb(22, 50, 58);
            _statusLabel.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, GraphicsUnit.Point);
            ((ModernTrayMenuItem)_statusLabel).MenuIcon?.Dispose();
            ((ModernTrayMenuItem)_statusLabel).MenuIcon = CreateMenuGlyph(MenuGlyph.Status, Color.FromArgb(25, 184, 137));
            _statusLabel.Tag = "header";

            _settingsMenuItem = CreateMenuItem("Settings", MenuGlyph.Settings);
            _settingsMenuItem.Click += (_, _) => _openSettingsAction.Invoke();

            _refreshMenuItem = CreateMenuItem("Refresh", MenuGlyph.Refresh);
            _refreshMenuItem.Click += (_, _) => _refreshAction.Invoke();

            _updatesMenuItem = CreateMenuItem("Check for updates", MenuGlyph.Update);
            _updatesMenuItem.Click += (_, _) => _checkUpdatesAction.Invoke();
            _languageMenuItem = CreateMenuItem("Language / 语言", MenuGlyph.Language);
            _languageMenuItem.Click += (_, _) => _openSettingsAction.Invoke();

            _openLogsMenuItem = CreateMenuItem("Open Logs Folder", MenuGlyph.Logs);
            _openLogsMenuItem.Click += (_, _) => OpenLogsFolder();

            _showMenuItem = CreateMenuItem("Show", MenuGlyph.Window);
            _showMenuItem.Click += (_, _) => { ShowWindow(); UpdateTrayIcon(); };

            _hideMenuItem = CreateMenuItem("Hide", MenuGlyph.Window);
            _hideMenuItem.Click += (_, _) => { HideWindow(); UpdateTrayIcon(); };

            _lockMenuItem = CreateMenuItem("Lock", MenuGlyph.Lock);
            _lockMenuItem.Click += (_, _) => { _lockAction.Invoke(); UpdateTrayIcon(); };

            _unlockMenuItem = CreateMenuItem("Unlock", MenuGlyph.Unlock);
            _unlockMenuItem.Click += (_, _) => { _unlockAction.Invoke(); UpdateTrayIcon(); };

            _exitMenuItem = CreateMenuItem("Exit", MenuGlyph.Exit, isDanger: true);
            _exitMenuItem.Click += (_, _) => ExitApplication();

            _contextMenu.Items.Add(_statusLabel);
            _contextMenu.Items.Add(CreateSeparator());
            _contextMenu.Items.Add(_settingsMenuItem);
            _contextMenu.Items.Add(_languageMenuItem);
            _contextMenu.Items.Add(_updatesMenuItem);
            _contextMenu.Items.Add(_refreshMenuItem);
            _contextMenu.Items.Add(_openLogsMenuItem);
            _contextMenu.Items.Add(CreateSeparator());
            _contextMenu.Items.Add(_showMenuItem);
            _contextMenu.Items.Add(_hideMenuItem);
            _contextMenu.Items.Add(CreateSeparator());
            _contextMenu.Items.Add(_lockMenuItem);
            _contextMenu.Items.Add(_unlockMenuItem);
            _contextMenu.Items.Add(CreateSeparator());
            _contextMenu.Items.Add(_exitMenuItem);

            _contextMenu.Opening += (_, _) =>
            {
                UpdateTrayIcon();
                LayoutMenu(_contextMenu);
                ApplyRoundedMenuRegion();
            };

            _notifyIcon.ContextMenuStrip = _contextMenu;
            UpdateMenuText();
        }

        internal static ContextMenuStrip CreateMenuShell() => new()
        {
            BackColor = Color.FromArgb(246, 250, 249),
            ForeColor = Color.FromArgb(22, 50, 58),
            Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, GraphicsUnit.Point),
            AutoSize = false, Padding = new Padding(6), ShowImageMargin = false, ShowCheckMargin = false,
            Renderer = new ModernTrayMenuRenderer(), DropShadowEnabled = true
        };

        internal static void LayoutMenu(ContextMenuStrip menu)
        {
            float scale = menu.DeviceDpi / 96F;
            int width = (int)Math.Ceiling(250 * scale);
            foreach (ToolStripItem item in menu.Items)
                if (item.Available && item is ToolStripMenuItem)
                    width = Math.Max(width, TextRenderer.MeasureText(item.Text, item.Font,
                        System.Drawing.Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + (int)Math.Ceiling(64 * scale));
            menu.Padding = new Padding((int)Math.Ceiling(6 * scale));
            int height = menu.Padding.Vertical;
            foreach (ToolStripItem item in menu.Items)
            {
                item.AutoSize = false;
                item.Size = new System.Drawing.Size(width, (int)Math.Ceiling(
                    (item is ToolStripSeparator ? 10 : Equals(item.Tag, "header") ? 42 : 38) * scale));
                if (item.Available) height += item.Height + item.Margin.Vertical;
            }
            menu.Size = new System.Drawing.Size(width + menu.Padding.Horizontal, height);
            menu.PerformLayout();
        }

        internal static ToolStripMenuItem CreateMenuItem(string text, MenuGlyph glyph, bool isDanger = false)
        {
            return new ModernTrayMenuItem(text)
            {
                AutoSize = false,
                ForeColor = isDanger ? Color.FromArgb(183, 61, 78) : Color.FromArgb(22, 50, 58),
                MenuIcon = CreateMenuGlyph(
                    glyph,
                    isDanger ? Color.FromArgb(183, 61, 78) : Color.FromArgb(92, 120, 127)),
                ImageScaling = ToolStripItemImageScaling.None,
                ImageAlign = ContentAlignment.MiddleCenter,
                TextAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                Tag = isDanger ? "danger" : null
            };
        }

        private static ToolStripSeparator CreateSeparator()
        {
            return new ToolStripSeparator
            {
                Margin = new Padding(0, 5, 0, 5)
            };
        }

        private void ApplyRoundedMenuRegion()
        {
            if (_contextMenu == null || _contextMenu.Width <= 0 || _contextMenu.Height <= 0) return;

            using var path = CreateRoundedRectanglePath(
                new Rectangle(0, 0, _contextMenu.Width, _contextMenu.Height), 10);
            Region? previousRegion = _contextMenu.Region;
            _contextMenu.Region = new Region(path);
            previousRegion?.Dispose();
        }

        private void UpdateMenuText()
        {
            if (_settingsMenuItem == null) return;

            _settingsMenuItem.Text = LanguageHelper.GetString("Lang_Tray_Settings");
            // 刷新按钮的多语言 key，如果没加翻译就默认显示 Refresh
            _refreshMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Refresh"); 
            _updatesMenuItem!.Text = LanguageHelper.GetString("Lang_Update_Check");
            _openLogsMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_OpenLogs");
            _showMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Show");
            _hideMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Hide");
            _lockMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Lock");
            _unlockMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Unlock");
            _exitMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Exit");
        }

        private Icon CreateIcon(Color color)
        {
            // 使用 2x 画布绘制浅色图标，再由系统缩放到托盘尺寸，边缘会更清晰。
            using var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.ScaleTransform(2F, 2F);

                using GraphicsPath tilePath = CreateRoundedRectanglePath(new Rectangle(1, 1, 14, 14), 3);
                using var tileBrush = new SolidBrush(Color.FromArgb(244, 248, 251));
                using var tileBorder = new Pen(Color.FromArgb(188, 211, 226), 0.75F);
                g.FillPath(tileBrush, tilePath);
                g.DrawPath(tileBorder, tilePath);

                using GraphicsPath bubblePath = CreateRoundedRectanglePath(new Rectangle(3, 3, 10, 9), 3);
                using var outlinePen = new Pen(Color.FromArgb(25, 50, 71), 1.2F)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                g.DrawPath(outlinePen, bubblePath);
                g.DrawLines(outlinePen, [new PointF(5.8F, 11.5F), new PointF(4.2F, 13.3F), new PointF(7.2F, 11.8F)]);

                using var statusBrush = new SolidBrush(color);
                using GraphicsPath shortBar = CreateRoundedRectanglePath(new Rectangle(5, 6, 2, 4), 1);
                using GraphicsPath tallBar = CreateRoundedRectanglePath(new Rectangle(7, 5, 2, 6), 1);
                using GraphicsPath mediumBar = CreateRoundedRectanglePath(new Rectangle(10, 6, 2, 4), 1);
                g.FillPath(statusBrush, shortBar);
                g.FillPath(statusBrush, tallBar);
                g.FillPath(statusBrush, mediumBar);
            }
            IntPtr iconHandle = bitmap.GetHicon();
            try
            {
                using Icon temporaryIcon = Icon.FromHandle(iconHandle);
                return (Icon)temporaryIcon.Clone();
            }
            finally
            {
                DestroyIcon(iconHandle);
            }
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

            if (_statusLabel != null)
            {
                Image? oldStatusImage = ((ModernTrayMenuItem)_statusLabel).MenuIcon;
                ((ModernTrayMenuItem)_statusLabel).MenuIcon = CreateMenuGlyph(MenuGlyph.Status, iconColor);
                oldStatusImage?.Dispose();
                _statusLabel.ToolTipText = _notifyIcon.Text;
            }

            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = CreateIcon(iconColor);
            oldIcon?.Dispose();

            if (_showMenuItem != null) _showMenuItem.Visible = !isVisible;
            if (_hideMenuItem != null) _hideMenuItem.Visible = isVisible;
            if (_lockMenuItem != null) _lockMenuItem.Visible = !isLocked;
            if (_unlockMenuItem != null) _unlockMenuItem.Visible = isLocked;
            
            UpdateMenuText();
        }

        private void ShowWindow() { _mainWindow.Show(); _mainWindow.Activate(); }
        private void HideWindow() => _mainWindow.Hide();

        private void OpenLogsFolder()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = AppLogger.LogDirectoryPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to open the logs folder.", ex);
                System.Windows.MessageBox.Show(
                    LanguageHelper.GetString("Lang_Log_OpenFailed"),
                    "TS6 Speaker Overlay",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static Bitmap CreateMenuGlyph(MenuGlyph glyph, Color color)
        {
            // Draw at 3x and reduce once, so small tray-menu strokes do not look jagged.
            using var master = new Bitmap(54, 54, PixelFormat.Format32bppPArgb);
            using Graphics graphics = Graphics.FromImage(master);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.ScaleTransform(3F, 3F);

            using var pen = new Pen(color, 1.8F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using var brush = new SolidBrush(color);

            switch (glyph)
            {
                case MenuGlyph.Status:
                    graphics.FillEllipse(brush, 5F, 5F, 8F, 8F);
                    break;
                case MenuGlyph.Settings:
                    graphics.DrawLine(pen, 3F, 5F, 15F, 5F);
                    graphics.DrawLine(pen, 3F, 9F, 15F, 9F);
                    graphics.DrawLine(pen, 3F, 13F, 15F, 13F);
                    graphics.FillEllipse(brush, 6F, 3F, 4F, 4F);
                    graphics.FillEllipse(brush, 11F, 7F, 4F, 4F);
                    graphics.FillEllipse(brush, 5F, 11F, 4F, 4F);
                    break;
                case MenuGlyph.Refresh:
                    graphics.DrawArc(pen, 3F, 3F, 12F, 12F, 35F, 285F);
                    graphics.FillPolygon(brush, new[] { new PointF(13.5F, 2F), new PointF(16F, 5.5F), new PointF(11.5F, 5.5F) });
                    break;
                case MenuGlyph.Logs:
                    graphics.DrawRectangle(pen, 4F, 2.5F, 10F, 13F);
                    graphics.DrawLine(pen, 6.5F, 6F, 11.5F, 6F);
                    graphics.DrawLine(pen, 6.5F, 9F, 11.5F, 9F);
                    graphics.DrawLine(pen, 6.5F, 12F, 10F, 12F);
                    break;
                case MenuGlyph.Window:
                    graphics.DrawRectangle(pen, 2.5F, 3.5F, 13F, 11F);
                    graphics.DrawLine(pen, 3F, 7F, 15F, 7F);
                    graphics.FillEllipse(brush, 4.5F, 5F, 1.5F, 1.5F);
                    break;
                case MenuGlyph.Lock:
                    graphics.DrawArc(pen, 5F, 2F, 8F, 9F, 180F, -180F);
                    graphics.DrawRectangle(pen, 4F, 8F, 10F, 7F);
                    graphics.FillEllipse(brush, 8F, 10.5F, 2F, 2F);
                    break;
                case MenuGlyph.Unlock:
                    graphics.DrawArc(pen, 7F, 2F, 7F, 9F, 185F, -145F);
                    graphics.DrawRectangle(pen, 4F, 8F, 10F, 7F);
                    graphics.FillEllipse(brush, 8F, 10.5F, 2F, 2F);
                    break;
                case MenuGlyph.Exit:
                    graphics.DrawLine(pen, 8F, 3F, 4F, 3F);
                    graphics.DrawLine(pen, 4F, 3F, 4F, 15F);
                    graphics.DrawLine(pen, 4F, 15F, 8F, 15F);
                    graphics.DrawLine(pen, 7F, 9F, 15F, 9F);
                    graphics.DrawLine(pen, 12F, 6F, 15F, 9F);
                    graphics.DrawLine(pen, 15F, 9F, 12F, 12F);
                    break;
                case MenuGlyph.Update:
                    graphics.DrawLine(pen, 9F, 2F, 9F, 12F);
                    graphics.DrawLines(pen, [new PointF(5F, 8F), new PointF(9F, 12F), new PointF(13F, 8F)]);
                    graphics.DrawLines(pen, [new PointF(3F, 12F), new PointF(3F, 15F), new PointF(15F, 15F), new PointF(15F, 12F)]);
                    break;
                case MenuGlyph.Language:
                    graphics.DrawEllipse(pen, 2F, 2F, 14F, 14F);
                    graphics.DrawEllipse(pen, 6F, 2F, 6F, 14F);
                    graphics.DrawLine(pen, 2F, 9F, 16F, 9F);
                    break;
            }

            graphics.Flush(FlushIntention.Sync);
            graphics.Dispose();
            return (Bitmap)master.Clone();
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal enum MenuGlyph
        {
            Status,
            Settings,
            Refresh,
            Logs,
            Window,
            Lock,
            Unlock,
            Exit,
            Update,
            Language
        }

        private sealed class ModernTrayMenuItem(string text) : ToolStripMenuItem(text)
        {
            // Keep custom glyphs out of the native Image property: WinForms can
            // otherwise reserve/clip its own image column even with margins off.
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            internal Image? MenuIcon { get; set; }
            protected override void Dispose(bool disposing)
            {
                if (disposing) { MenuIcon?.Dispose(); MenuIcon = null; }
                base.Dispose(disposing);
            }
        }

        private sealed class ModernTrayMenuRenderer : ToolStripProfessionalRenderer
        {
            public ModernTrayMenuRenderer() : base(new ModernTrayColorTable())
            {
                RoundedEdges = true;
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Selected || Equals(e.Item.Tag, "header"))
                {
                    using var backgroundBrush = new SolidBrush(e.ToolStrip?.BackColor ?? Color.FromArgb(246, 250, 249));
                    e.Graphics.FillRectangle(backgroundBrush, new Rectangle(0, 0, e.Item.Width, e.Item.Height));
                    return;
                }

                Color background = Equals(e.Item.Tag, "danger")
                    ? Color.FromArgb(251, 232, 234)
                    : Color.FromArgb(222, 244, 236);
                using var brush = new SolidBrush(background);
                using GraphicsPath path = CreateRoundedRectanglePath(
                    new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2), 6);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using var pen = new Pen(Color.FromArgb(214, 228, 230));
                int y = e.Item.Height / 2;
                var layout = TrayMenuLayout.Calculate(e.Item.Width, e.Item.Height, e.Graphics.DpiX);
                e.Graphics.DrawLine(pen, layout.Text.Left, y, layout.Text.Right, y);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                var layout = TrayMenuLayout.Calculate(e.Item.Width, e.Item.Height, e.Graphics.DpiX);
                var state = e.Graphics.Save();
                e.Graphics.SetClip(new Rectangle(0, 0, e.Item.Width, e.Item.Height), CombineMode.Replace);
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using var brush = new SolidBrush(e.Item.ForeColor);
                using var format = new StringFormat(StringFormat.GenericTypographic)
                {
                    Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.None
                };
                e.Graphics.DrawString(e.Text, e.TextFont ?? e.Item.Font, brush, layout.Text, format);
                e.Graphics.Restore(state);
                DrawItemIcon(e.Graphics, e.Item);
            }

            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
            protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e) { }

            private static void DrawItemIcon(Graphics graphics, ToolStripItem item)
            {
                if (item is not ModernTrayMenuItem { MenuIcon: not null } menuItem) return;
                var layout = TrayMenuLayout.Calculate(item.Width, item.Height, graphics.DpiX);
                var state = graphics.Save();
                graphics.SetClip(new Rectangle(0, 0, item.Width, item.Height), CombineMode.Replace);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(menuItem.MenuIcon, layout.Icon);
                graphics.Restore(state);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using var pen = new Pen(Color.FromArgb(200, 218, 220));
                using GraphicsPath path = CreateRoundedRectanglePath(
                    new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 10);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
        }

        private sealed class ModernTrayColorTable : ProfessionalColorTable
        {
            private static readonly Color Background = Color.FromArgb(246, 250, 249);
            private static readonly Color Selected = Color.FromArgb(222, 244, 236);
            private static readonly Color Border = Color.FromArgb(200, 218, 220);

            public override Color ToolStripDropDownBackground => Background;
            public override Color ImageMarginGradientBegin => Background;
            public override Color ImageMarginGradientMiddle => Background;
            public override Color ImageMarginGradientEnd => Background;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => Selected;
            public override Color MenuItemSelected => Selected;
            public override Color MenuItemSelectedGradientBegin => Selected;
            public override Color MenuItemSelectedGradientEnd => Selected;
            public override Color SeparatorDark => Color.FromArgb(214, 228, 230);
            public override Color SeparatorLight => Color.FromArgb(214, 228, 230);
        }

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
            LanguageHelper.LanguageChanged -= UpdateMenuText;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                var icon = _notifyIcon.Icon;
                _notifyIcon.Icon = null;
                icon?.Dispose();
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            if (_contextMenu != null)
                foreach (ToolStripItem item in _contextMenu.Items) item.Image?.Dispose();
            _contextMenu?.Dispose();
            _contextMenu = null;
        }
    }

    internal static class TrayMenuLayout
    {
        internal static (Rectangle Icon, Rectangle Text) Calculate(int width, int height, float dpi)
        {
            float scale = dpi / 96F;
            int iconSize = (int)Math.Round(18 * scale);
            int textLeft = (int)Math.Round(44 * scale);
            return (new Rectangle((int)Math.Round(14 * scale), (height - iconSize) / 2, iconSize, iconSize),
                new Rectangle(textLeft, 0, Math.Max(1, width - textLeft - (int)Math.Round(16 * scale)), height));
        }
    }
}
