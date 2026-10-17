using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace TS6_SpeakerOverlay.Models
{
    // 显示模式使用显式数值，避免新增选项改变旧配置的含义。
    public enum AvatarMode
    {
        Avatar = 0,     // 显示头像 (默认)
        Indicator = 1,  // 仅指示灯 (纯色圆点)
        None = 2,       // 纯文字 (隐藏左侧圆圈)
        AvatarOnly = 3  // 仅显示头像，状态以头像角标呈现
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
        [ObservableProperty] private bool _enableServerNotifications = true;
        [ObservableProperty] private bool _enableChannelNotifications = true;
        [ObservableProperty] private bool _showOnlyTalking = false;
        [ObservableProperty] private bool _isLocked = false;
        [ObservableProperty] private bool _autoStart = false;
        [ObservableProperty] private bool _autoOpenWithTeamSpeak = false;
        [ObservableProperty] private bool _checkUpdatesOnStartup = true;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TalkingBrush))]
        [NotifyPropertyChangedFor(nameof(TalkingSurfaceBrush))]
        [NotifyPropertyChangedFor(nameof(TalkingGlowEffect))]
        private string _talkingColor = "#19B889";
        [ObservableProperty] private string _language = "zh-CN";
        [ObservableProperty] private string _selectedServerUniqueIdentifier = string.Empty;
        [ObservableProperty] private int _remoteAppPort = 5899;

        [JsonIgnore]
        public SolidColorBrush TalkingBrush => new(GetTalkingColor());

        [JsonIgnore]
        public SolidColorBrush TalkingSurfaceBrush
        {
            get
            {
                var color = GetTalkingColor();
                return new SolidColorBrush(MediaColor.FromArgb(0x32, color.R, color.G, color.B));
            }
        }

        [JsonIgnore]
        public System.Windows.Media.Effects.DropShadowEffect TalkingGlowEffect => new()
        {
            Color = GetTalkingColor(), BlurRadius = 7, ShadowDepth = 0, Opacity = 0.72
        };

        private MediaColor GetTalkingColor()
        {
            try
            {
                return (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(TalkingColor);
            }
            catch
            {
                return MediaColor.FromRgb(0x19, 0xB8, 0x89);
            }
        }

        public void ResetDefaults()
        {
            UiScale = 1.0;
            BackgroundOpacity = 0.6;
            FontSize = 14.0;
            ItemSpacing = 4.0;
            AvatarSize = 20.0;
            AvatarDisplayMode = AvatarMode.Avatar; // 默认显示头像
            EnableNotifications = true;
            EnableServerNotifications = true;
            EnableChannelNotifications = true;
            ShowOnlyTalking = false;
            IsLocked = false;
            AutoStart = false;
            AutoOpenWithTeamSpeak = false;
            CheckUpdatesOnStartup = true;
            TalkingColor = "#19B889";
            SelectedServerUniqueIdentifier = string.Empty;
            RemoteAppPort = 5899;
        }
    }
}
