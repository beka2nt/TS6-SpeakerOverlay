using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading; // 需要 Dispatcher
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Services;

namespace TS6_SpeakerOverlay.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // UI 绑定的用户列表
        public ObservableCollection<User> Users { get; } = new();
        
        private readonly Ts6Service _tsService;
        private string _currentChannelId = ""; // 当前所在的频道

        [ObservableProperty] private bool _isOverlayLocked = false;

        public MainViewModel()
        {
            _tsService = new Ts6Service();
            
            // 订阅事件：初始化用户列表
            _tsService.OnChannelListUpdated += (allUsers, myChannelId) => 
            {
                _currentChannelId = myChannelId;
                
                // 必须在 UI 线程更新 ObservableCollection
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Users.Clear();
                    // 只显示和我同频道的用户
                    var roomUsers = allUsers.Where(u => u.ChannelId == _currentChannelId).ToList();
                    foreach(var u in roomUsers) Users.Add(u);
                    
                    Console.WriteLine($"[UI] 已更新列表，当前频道人数: {Users.Count}");
                });
            };

            // 订阅事件：说话状态改变
            _tsService.OnTalkStatusChanged += (clientId, isTalking) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 在当前列表中找这个人
                    var user = Users.FirstOrDefault(u => u.ClientId == clientId);
                    if (user != null)
                    {
                        user.IsTalking = isTalking;
                        // Console.WriteLine($"[UI] {user.Name} 说话: {isTalking}");
                    }
                });
            };

            // 启动连接
            Task.Run(async () => await _tsService.StartAsync());

            // 初始占位符
            Users.Add(new User { Name = "正在连接 TS6..." });
        }

        [RelayCommand]
        private void ToggleLockState(Window window)
        {
            IsOverlayLocked = !IsOverlayLocked;
            if (IsOverlayLocked) WindowHelper.EnableClickThrough(window);
            else WindowHelper.DisableClickThrough(window);
        }
    }
}