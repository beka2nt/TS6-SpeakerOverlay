using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Services;

namespace TS6_SpeakerOverlay.ViewModels
{
    public partial class MainViewModel : ObservableObject, System.IDisposable
    {
        public ObservableCollection<User> Users { get; } = new();
        public ObservableCollection<Notification> Notifications { get; } = new();
        public ObservableCollection<Ts6Server> Servers { get; } = new();
        
        public AppConfig Config { get; }
        public IReadOnlyList<int> RemoteAppPorts { get; } = [5899, 6100, 6200, 7000];

        private readonly Ts6Service _tsService;
        private int _currentConnectionId;
        private string _currentChannelId = "";
        private bool _isUpdatingServerSelection;

        [ObservableProperty] private bool _isOverlayLocked = false;
        
        // 连接状态文本
        [ObservableProperty] private string _connectionStatus = "Connecting...";
        [ObservableProperty] private bool _isConnected = false;
        [ObservableProperty] private Ts6Server? _selectedServer;

        public MainViewModel()
        {
            Config = ConfigService.Load();
            LanguageHelper.SetLanguage(Config.Language); 

            _tsService = new Ts6Service(Config.RemoteAppPort);
            
            _tsService.OnConnectionStateChanged += async (isConnected) =>
            {
                if (isConnected)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsConnected = true;
                        ConnectionStatus = "";
                    });
                    return;
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() => IsConnected = false);
                await Task.Delay(500);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (IsConnected) return;

                    ConnectionStatus = LanguageHelper.GetString("Lang_Status_RemoteUnavailable");
                    Users.Clear();
                    Servers.Clear();
                    _currentConnectionId = 0;
                    _currentChannelId = "";
                    _isUpdatingServerSelection = true;
                    SelectedServer = null;
                    _isUpdatingServerSelection = false;
                });
            };

            _tsService.OnServersUpdated += servers =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => UpdateServers(servers));
            };

            _tsService.OnTalkStatusChanged += (connectionId, clientId, isTalking) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    User? cachedUser = Servers.FirstOrDefault(server => server.ConnectionId == connectionId)?
                        .Users.FirstOrDefault(user => user.ClientId == clientId);
                    if (cachedUser != null) cachedUser.IsTalking = isTalking;

                    if (connectionId != _currentConnectionId) return;
                    User? visibleUser = Users.FirstOrDefault(user => user.ClientId == clientId);
                    if (visibleUser != null) visibleUser.IsTalking = isTalking;
                });
            };

            _tsService.OnUserPropertiesChanged += (connectionId, clientId, inputMuted, outputMuted, away) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    User? cachedUser = Servers.FirstOrDefault(server => server.ConnectionId == connectionId)?
                        .Users.FirstOrDefault(user => user.ClientId == clientId);
                    ApplyUserProperties(cachedUser, inputMuted, outputMuted, away);

                    if (connectionId != _currentConnectionId) return;
                    User? visibleUser = Users.FirstOrDefault(user => user.ClientId == clientId);
                    ApplyUserProperties(visibleUser, inputMuted, outputMuted, away);
                });
            };

            _tsService.OnServerMembershipChanged += (connectionId, clientId, clientName, joined) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (connectionId != _currentConnectionId || !Config.EnableServerNotifications) return;
                    Ts6Server? server = Servers.FirstOrDefault(item => item.ConnectionId == connectionId);
                    if (server == null || clientId == server.MyClientId) return;

                    string name = string.IsNullOrWhiteSpace(clientName)
                        ? LanguageHelper.GetString("Lang_Notify_UnknownMember")
                        : clientName;
                    string key = joined ? "Lang_Notify_ServerJoined" : "Lang_Notify_ServerLeft";
                    ShowNotification(string.Format(LanguageHelper.GetString(key), name),
                        joined ? "#4FCD8E" : "#ED6A72", joined ? "●" : "○");
                });
            };

            _tsService.OnClientMoved += (connectionId, clientId, newCh, oldCh, clientName) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (connectionId != _currentConnectionId) return;

                    Ts6Server? server = Servers.FirstOrDefault(item => item.ConnectionId == connectionId);
                    User? cachedUser = server?.Users.FirstOrDefault(user => user.ClientId == clientId);
                    if (cachedUser != null) cachedUser.ChannelId = newCh;

                    if (server != null && clientId == server.MyClientId)
                    {
                        server.MyChannelId = newCh;
                        _currentChannelId = newCh;
                        ShowSelectedServer();
                        return;
                    }

                    string name = clientName ?? cachedUser?.Name ??
                        LanguageHelper.GetString("Lang_Notify_UnknownMember");
                    bool cameFromOutsideServer = string.IsNullOrEmpty(oldCh) || oldCh == "0";
                    bool wentOutsideServer = string.IsNullOrEmpty(newCh) || newCh == "0";
                    if (cameFromOutsideServer || wentOutsideServer) return;
                    if (newCh == oldCh) return;

                    if (newCh == _currentChannelId)
                    {
                        if (cachedUser != null && Users.All(user => user.ClientId != clientId))
                        {
                            InsertVisibleUserSorted(cachedUser);
                        }
                        if (Config.EnableChannelNotifications)
                            ShowNotification(string.Format(LanguageHelper.GetString("Lang_Notify_ChannelJoined"), name),
                                "#4FCD8E", "↳");
                    }
                    else if (oldCh == _currentChannelId)
                    {
                        User? user = Users.FirstOrDefault(item => item.ClientId == clientId);
                        if (user != null) Users.Remove(user);
                        if (Config.EnableChannelNotifications)
                            ShowNotification(string.Format(LanguageHelper.GetString("Lang_Notify_ChannelLeft"), name),
                                "#ED6A72", "↰");
                    }
                });
            };

            _ = _tsService.StartAsync();
        }

        partial void OnSelectedServerChanged(Ts6Server? value)
        {
            if (_isUpdatingServerSelection) return;

            if (value != null)
            {
                Config.SelectedServerUniqueIdentifier = value.UniqueIdentifier;
            }

            ShowSelectedServer();
        }

        private void UpdateServers(IReadOnlyList<Ts6Server> servers)
        {
            int previousConnectionId = _currentConnectionId;
            Dictionary<int, bool> talkingStates = Users.ToDictionary(user => user.ClientId, user => user.IsTalking);

            Servers.Clear();
            foreach (Ts6Server server in servers.OrderBy(server => server.Name))
            {
                Servers.Add(server);
            }

            Ts6Server? selected = Servers.FirstOrDefault(server =>
                server.UniqueIdentifier == Config.SelectedServerUniqueIdentifier);
            selected ??= Servers.FirstOrDefault();

            _isUpdatingServerSelection = true;
            SelectedServer = selected;
            _isUpdatingServerSelection = false;

            if (selected != null && string.IsNullOrEmpty(Config.SelectedServerUniqueIdentifier))
            {
                Config.SelectedServerUniqueIdentifier = selected.UniqueIdentifier;
            }

            ShowSelectedServer(previousConnectionId == selected?.ConnectionId ? talkingStates : null);
        }

        private void ShowSelectedServer(IReadOnlyDictionary<int, bool>? talkingStates = null)
        {
            Users.Clear();
            _currentConnectionId = SelectedServer?.ConnectionId ?? 0;
            _currentChannelId = SelectedServer?.MyChannelId ?? "";

            if (SelectedServer == null)
            {
                ConnectionStatus = LanguageHelper.GetString("Lang_Status_Waiting");
                return;
            }

            ConnectionStatus = "";
            foreach (User user in SelectedServer.Users
                .Where(user => user.ChannelId == _currentChannelId)
                .OrderBy(user => user.Name))
            {
                if (talkingStates?.TryGetValue(user.ClientId, out bool isTalking) == true)
                {
                    user.IsTalking = isTalking;
                }
                Users.Add(user);
            }
        }

        public void ApplyConfiguredServerSelection()
        {
            Ts6Server? selected = Servers.FirstOrDefault(server =>
                server.UniqueIdentifier == Config.SelectedServerUniqueIdentifier);
            selected ??= Servers.FirstOrDefault();

            _isUpdatingServerSelection = true;
            SelectedServer = selected;
            _isUpdatingServerSelection = false;

            if (selected != null && string.IsNullOrEmpty(Config.SelectedServerUniqueIdentifier))
            {
                Config.SelectedServerUniqueIdentifier = selected.UniqueIdentifier;
            }

            ShowSelectedServer();
        }

        // [新增] 手动刷新方法
        public void RefreshData()
        {
            if (!IsConnected)
            {
                ConnectionStatus = LanguageHelper.GetString("Lang_Status_RemoteUnavailable");
                return;
            }

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

        private static void ApplyUserProperties(User? user, bool? inputMuted, bool? outputMuted, bool? away)
        {
            if (user == null) return;
            if (inputMuted.HasValue) user.IsInputMuted = inputMuted.Value;
            if (outputMuted.HasValue) user.IsOutputMuted = outputMuted.Value;
            if (away.HasValue) user.IsAway = away.Value;
        }

        private void InsertVisibleUserSorted(User user)
        {
            int index = 0;
            while (index < Users.Count &&
                   string.Compare(Users[index].Name, user.Name, StringComparison.CurrentCultureIgnoreCase) <= 0)
            {
                index++;
            }
            Users.Insert(index, user);
        }

        [RelayCommand]
        private void ToggleLockState(Window window)
        {
            IsOverlayLocked = !IsOverlayLocked;
            if (IsOverlayLocked) WindowHelper.EnableClickThrough(window);
            else WindowHelper.DisableClickThrough(window);
        }
        
        public void SaveConfig() => ConfigService.Save(Config);

        public void Dispose() => _tsService.Dispose();
    }
}
