using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;

namespace TS6_SpeakerOverlay.Models
{
    public partial class User : ObservableObject
    {
        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(Initials))] // 名字变了，首字母也要变
        private string _name = string.Empty;

        [ObservableProperty] private bool _isTalking;
        [ObservableProperty] private string _avatarUrl = string.Empty; 

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