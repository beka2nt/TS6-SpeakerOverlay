using CommunityToolkit.Mvvm.ComponentModel;

namespace TS6_SpeakerOverlay.Models
{
    // 定义三种显示模式
    public enum AvatarMode
    {
        Avatar,    // 显示头像 (默认)
        Indicator, // 仅指示灯 (纯色圆点)
        None       // 纯文字 (隐藏左侧圆圈)
    }

    public partial class AppConfig : ObservableObject
    {
        [ObservableProperty] private double _windowTop = 50;
        [ObservableProperty] private double _windowLeft = 50;

        [ObservableProperty] private double _uiScale = 1.0; 
        [ObservableProperty] private double _backgroundOpacity = 0.6;
        [ObservableProperty] private double _fontSize = 14.0;
        [ObservableProperty] private double _itemSpacing = 4.0;
        [ObservableProperty] private double _avatarSize = 20.0;
        
        // [修改] 将 bool 改为枚举，支持三种模式
        [ObservableProperty] private AvatarMode _avatarDisplayMode = AvatarMode.Avatar;

        [ObservableProperty] private bool _enableNotifications = true;
        [ObservableProperty] private bool _showOnlyTalking = false;
        [ObservableProperty] private string _language = "zh-CN";

        public void ResetDefaults()
        {
            UiScale = 1.0;
            BackgroundOpacity = 0.6;
            FontSize = 14.0;
            ItemSpacing = 4.0;
            AvatarSize = 20.0;
            AvatarDisplayMode = AvatarMode.Avatar; // 默认显示头像
            EnableNotifications = true;
            ShowOnlyTalking = false;
        }
    }
}