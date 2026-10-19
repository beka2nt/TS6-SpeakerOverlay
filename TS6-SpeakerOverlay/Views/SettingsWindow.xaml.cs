using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls; // 需要引用 ComboBox
using System.Windows.Input;
using TS6_SpeakerOverlay.ViewModels;
using TS6_SpeakerOverlay.Helpers; // 引用 LanguageHelper
using TS6_SpeakerOverlay.Services;

namespace TS6_SpeakerOverlay.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private bool _isInitialized = false;

        public SettingsWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;
            _isInitialized = true;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            //_viewModel.SaveConfig();
            this.Close();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _viewModel.RefreshData();

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            //_viewModel.SaveConfig(); 
            this.Close();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                GetResourceText("Lang_Reset_Confirm_Message", "Reset all settings to default?"),
                GetResourceText("Lang_Reset_Confirm_Title", "Confirm"),
                System.Windows.MessageBoxButton.YesNo, 
                System.Windows.MessageBoxImage.Question
            );

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _viewModel.Config.ResetDefaults();
                if (_viewModel.IsOverlayLocked && System.Windows.Application.Current.MainWindow is Window overlay)
                    _viewModel.ToggleLockStateCommand.Execute(overlay);
                // 重置后也要重新应用语言（因为默认可能是中文）
                LanguageHelper.SetLanguage(_viewModel.Config.Language);
                _viewModel.ApplyConfiguredServerSelection();
            }
        }

        // [新增] 语言切换事件
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return; // 防止初始化时误触发

            if (DataContext is MainViewModel vm)
            {
                // 获取选中的 Tag (en-US, zh-CN...)
                if (e.AddedItems.Count > 0 && e.AddedItems[0] is LanguageHelper.LanguageOption item)
                {
                    string langCode = item.Code;
                    // 1. 更新 Config 对象
                    vm.Config.Language = langCode;
                    // 2. 立即应用资源字典
                    LanguageHelper.SetLanguage(langCode);
                }
            }
        }

        private void BtnOpenLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppLogger.LogDirectoryPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to open the logs directory from settings.", ex);
                System.Windows.MessageBox.Show(
                    GetResourceText("Lang_Log_OpenFailed", "Unable to open the logs directory."),
                    GetResourceText("Lang_Diagnostics", "Diagnostics"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void BtnViewLogs_Click(object sender, RoutedEventArgs e)
        {
            new LogViewerWindow { Owner = this }.ShowDialog();
        }

        private void TalkingColorPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button { Tag: string color })
                _viewModel.Config.TalkingColor = color;
        }

        private void TalkingColorCustom_Click(object sender, RoutedEventArgs e)
        {
            System.Drawing.Color initialColor;
            try
            {
                initialColor = System.Drawing.ColorTranslator.FromHtml(_viewModel.Config.TalkingColor);
            }
            catch
            {
                initialColor = System.Drawing.ColorTranslator.FromHtml("#19B889");
            }

            using var dialog = new System.Windows.Forms.ColorDialog
            {
                AllowFullOpen = true,
                FullOpen = true,
                Color = initialColor
            };
            var owner = new ColorDialogOwner(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            if (dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK)
                _viewModel.Config.TalkingColor = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }

        private sealed class ColorDialogOwner(IntPtr handle) : System.Windows.Forms.IWin32Window
        {
            public IntPtr Handle { get; } = handle;
        }

        private static string GetResourceText(string key, string fallback)
        {
            return System.Windows.Application.Current?.TryFindResource(key) as string ?? fallback;
        }
    }
}
