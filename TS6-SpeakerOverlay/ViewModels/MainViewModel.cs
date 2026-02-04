using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Services;

namespace TS6_SpeakerOverlay.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public ObservableCollection<User> Users { get; } = new();
        public ObservableCollection<Notification> Notifications { get; } = new();
        
        public AppConfig Config { get; }

        private readonly Ts6Service _tsService;
        private string _currentChannelId = ""; 

        [ObservableProperty] private bool _isOverlayLocked = false;
        
        // 连接状态文本
        [ObservableProperty] private string _connectionStatus = "Connecting...";
        [ObservableProperty] private bool _isConnected = false;

        public MainViewModel()
        {
            Config = ConfigService.Load();
            LanguageHelper.SetLanguage(Config.Language); 

            // [新增] 启动时恢复锁定状态
            IsOverlayLocked = Config.IsLocked;

            _tsService = new Ts6Service();
            
                // 1. 连接状态变化
            _tsService.OnConnectionStateChanged += async (isConnected) =>
            {
                if (isConnected)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsConnected = true;
                        ConnectionStatus = ""; // 连上瞬间清空提示
                    });
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => IsConnected = false);

                    // [极速优化] 将防抖动延迟从 2000 改为 500
                    // 0.5秒足够过滤掉网络波动，同时让用户感觉反应很快
                    await Task.Delay(500);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!IsConnected)
                        {
                            ConnectionStatus = LanguageHelper.GetString("Lang_Status_Waiting");
                            Users.Clear();
                        }
                    });
                }
            };

            // 2. 列表更新 (核心修改)
            _tsService.OnChannelListUpdated += (allUsers, myChannelId) => 
            {
                _currentChannelId = myChannelId;
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    // 暂存说话状态
                    var talkingStates = Users.ToDictionary(u => u.ClientId, u => u.IsTalking);

                    Users.Clear();
                    
                    // [新增] 判断列表是否为空
                    if (allUsers.Count == 0)
                    {
                        // 如果列表为空，说明连上了 TS6 但没进服务器
                        ConnectionStatus = LanguageHelper.GetString("Lang_Status_Waiting");
                    }
                    else
                    {
                        // 列表有人，清空提示
                        ConnectionStatus = "";
                        
                        var roomUsers = allUsers.Where(u => u.ChannelId == _currentChannelId).OrderBy(u => u.Name);
                        foreach(var u in roomUsers) 
                        {
                            if (talkingStates.ContainsKey(u.ClientId)) u.IsTalking = talkingStates[u.ClientId];
                            Users.Add(u);
                        }
                    }
                });
            };

            _tsService.OnTalkStatusChanged += (clientId, isTalking) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var user = Users.FirstOrDefault(u => u.ClientId == clientId);
                    if (user != null) user.IsTalking = isTalking;
                });
            };

            _tsService.OnUserPropertiesChanged += (clientId, inMute, outMute, away) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var user = Users.FirstOrDefault(u => u.ClientId == clientId);
                    if (user != null)
                    {
                        if (inMute.HasValue) user.IsInputMuted = inMute.Value;
                        if (outMute.HasValue) user.IsOutputMuted = outMute.Value;
                        if (away.HasValue) user.IsAway = away.Value;
                    }
                });
            };

            _tsService.OnClientMoved += (clientId, newCh, oldCh) => 
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    if (newCh == _currentChannelId)
                    {
                        ShowNotification("Someone joined", "#4FCD8E", "📥");
                    }
                    else if (oldCh == _currentChannelId)
                    {
                        var user = Users.FirstOrDefault(u => u.ClientId == clientId);
                        string name = user?.Name ?? "Someone";
                        ShowNotification($"{name} left", "#ED4245", "📤");
                    }
                });
            };

            Task.Run(async () => await _tsService.StartAsync());
        }

        // [新增] 手动刷新方法
        public void RefreshData()
        {
            // 给用户一个瞬间反馈，证明他点到了
            ConnectionStatus = LanguageHelper.GetString("Lang_Status_Refreshing");

            _tsService.SendAuth();
        }

        private async void ShowNotification(string msg, string color, string icon)
        {
            if (!Config.EnableNotifications) return;
            var note = new Notification { Message = msg, Color = color, Icon = icon };
            Notifications.Add(note);
            await Task.Delay(3000);
            if (Notifications.Contains(note)) Notifications.Remove(note);
        }

        [RelayCommand]
        private void ToggleLockState(Window window)
        {
            IsOverlayLocked = !IsOverlayLocked;

            Config.IsLocked = IsOverlayLocked;
            
            if (IsOverlayLocked) WindowHelper.EnableClickThrough(window);
            else WindowHelper.DisableClickThrough(window);
        }
        
        public void SaveConfig() => ConfigService.Save(Config);
    }
}