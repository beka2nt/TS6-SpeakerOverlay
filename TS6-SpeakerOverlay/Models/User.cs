using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TS6_SpeakerOverlay.Models
{
    public partial class User : ObservableObject
    {
        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(Initials))] // 名字变了，首字母也要变
        private string _name = string.Empty;

        [ObservableProperty] private bool _isTalking;
        [ObservableProperty] private bool _isTalkerVisible;
        private CancellationTokenSource? _talkVisibilityDelay;

        partial void OnIsTalkingChanging(bool value)
        {
            if (!value) return;

            _talkVisibilityDelay?.Cancel();
            _talkVisibilityDelay?.Dispose();
            _talkVisibilityDelay = null;
            IsTalkerVisible = true;
        }

        partial void OnIsTalkingChanged(bool value)
        {
            LastTalkTime = value ? DateTime.UtcNow : LastTalkTime;
            if (value) return;

            _talkVisibilityDelay?.Cancel();
            _talkVisibilityDelay?.Dispose();
            _talkVisibilityDelay = new CancellationTokenSource();
            _ = HideAfterSpeechAsync(_talkVisibilityDelay.Token);
        }

        private async Task HideAfterSpeechAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(260, cancellationToken);
                if (!IsTalking) IsTalkerVisible = false;
            }
            catch (OperationCanceledException)
            {
                // 说话在淡出期间恢复时，保留当前成员并反向渐亮。
            }
        }
        [ObservableProperty] private string _clientUniqueIdentifier = string.Empty;
        [ObservableProperty] private string _serverAvatarPath = string.Empty;

        // 在 User 类中添加：

// 在 User 类中添加：

    public string NameColor 
    {
        get 
        {
            if (string.IsNullOrEmpty(Name)) return "#72767d"; // 默认灰
            
            // 简单的哈希算法，把名字转成颜色
            int hash = Name.GetHashCode();
            // 预设一组好看的 Discord 风格颜色
            string[] colors = new[] 
            { 
                "#EB459E", "#F1C40F", "#E91E63", "#9B59B6", 
                "#3498DB", "#1ABC9C", "#E67E22", "#E74C3C" 
            };
            
            // 取绝对值防止负数，然后取模
            int index = Math.Abs(hash) % colors.Length;
            return colors[index];
        }
    }

        // [新增] 获取名字首字母 (用于无头像时显示)
        public string Initials 
        {
            get 
            {
                if (string.IsNullOrEmpty(Name)) return "?";
                // 取前1-2个字符，转大写
                return Name.Length > 1 
                    ? Name.Substring(0, 1).ToUpper() 
                    : Name.ToUpper();
            }
        }

        [ObservableProperty] private bool _isInputMuted;
        [ObservableProperty] private bool _isOutputMuted;
        [ObservableProperty] private bool _isAway;

        [ObservableProperty] private int _clientId;
        [ObservableProperty] private string _channelId = string.Empty;

        public DateTime LastTalkTime { get; set; } = DateTime.MinValue;
    }
}
