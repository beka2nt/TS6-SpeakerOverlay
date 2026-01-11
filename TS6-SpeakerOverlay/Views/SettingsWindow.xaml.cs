using System.Windows;
using System.Windows.Controls; // 需要引用 ComboBox
using System.Windows.Input;
using TS6_SpeakerOverlay.ViewModels;
using TS6_SpeakerOverlay.Helpers; // 引用 LanguageHelper

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
            _viewModel.SaveConfig();
            this.Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveConfig(); 
            this.Close();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "Reset all settings to default?", 
                "Confirm", 
                System.Windows.MessageBoxButton.YesNo, 
                System.Windows.MessageBoxImage.Question
            );

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _viewModel.Config.ResetDefaults();
                // 重置后也要重新应用语言（因为默认可能是中文）
                LanguageHelper.SetLanguage(_viewModel.Config.Language);
            }
        }

        // [新增] 语言切换事件
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return; // 防止初始化时误触发

            if (DataContext is MainViewModel vm)
            {
                // 获取选中的 Tag (en-US, zh-CN...)
                if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item)
                {
                    string langCode = item.Tag.ToString() ?? "en-US";
                    // 1. 更新 Config 对象
                    vm.Config.Language = langCode;
                    // 2. 立即应用资源字典
                    LanguageHelper.SetLanguage(langCode);
                }
            }
        }
    }
}