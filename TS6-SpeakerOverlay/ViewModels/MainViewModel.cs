using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
        private CancellationTokenSource? _manualRefreshCancellation;
        private bool _isDisposed;
        private readonly UpdateService _updateService;
        private readonly CancellationTokenSource _updateLifetime = new();
        private ReleaseInfo? _latestRelease;
        public string CurrentVersion => UpdateService.CurrentVersion;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UpdateStatusText))]
        private UpdateCheckState _updateCheckState;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UpdateStatusText))]
        private string _latestVersion = "";
        [ObservableProperty] private bool _isUpdateAvailable;
        public string UpdateStatusText => UpdateCheckState switch
        {
            UpdateCheckState.Checking => LanguageHelper.GetString("Lang_Update_Checking"),
            UpdateCheckState.Current => string.Format(LanguageHelper.GetString("Lang_Update_Current"), CurrentVersion),
            UpdateCheckState.Available => string.Format(LanguageHelper.GetString("Lang_Update_Available"), LatestVersion),
            UpdateCheckState.Failed => LanguageHelper.GetString("Lang_Update_Failed"),
            UpdateCheckState.RateLimited => LanguageHelper.GetString("Lang_Update_RateLimited"),
            _ => string.Format(LanguageHelper.GetString("Lang_Update_NotChecked"), CurrentVersion)
        };

        [ObservableProperty] private bool _isOverlayLocked = false;
        
        // 连接状态文本
        [ObservableProperty] private string _connectionStatus = "Connecting...";
        [ObservableProperty] private bool _isConnected = false;
        [ObservableProperty] private Ts6Server? _selectedServer;

        public MainViewModel() : this(ConfigService.Load(), null)
        {
        }

        internal MainViewModel(AppConfig config, Ts6Service? service, bool startService = true, UpdateService? updateService = null)
        {
            Config = config;
            LanguageHelper.SetLanguage(Config.Language); 
            if (Config.Language != LanguageHelper.CurrentLanguage) Config.Language = LanguageHelper.CurrentLanguage;
            IsOverlayLocked = Config.IsLocked;
            _updateService = updateService ?? new UpdateService();
            LanguageHelper.LanguageChanged += OnLanguageChanged;

            _tsService = service ?? new Ts6Service(Config.RemoteAppPort);
            
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

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CancelManualRefresh();
                    IsConnected = false;
                });
                await Task.Delay(500);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_isDisposed || IsConnected) return;

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

            // Local movement/avatar updates are not evidence that a refresh replied.
            _tsService.OnSnapshotReceived += () =>
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_isDisposed) return;
                    CancelManualRefresh();
                    ConnectionStatus = SelectedServer == null
                        ? LanguageHelper.GetString("Lang_Status_Waiting") : "";
                });

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

            if (startService)
            {
                _ = _tsService.StartAsync();
                if (Config.CheckUpdatesOnStartup) _ = CheckForUpdatesCommand.ExecuteAsync(null);
            }
        }

        private void OnLanguageChanged() => OnPropertyChanged(nameof(UpdateStatusText));

        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            if (_isDisposed || UpdateCheckState == UpdateCheckState.Checking) return;
            UpdateCheckState = UpdateCheckState.Checking;
            IsUpdateAvailable = false;
            _latestRelease = null;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_updateLifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(12));
                ReleaseInfo release = await _updateService.GetLatestAsync(timeout.Token);
                if (_isDisposed) return;
                _latestRelease = release;
                LatestVersion = release.Tag;
                IsUpdateAvailable = UpdateService.IsNewer(release.Version, Version.Parse(CurrentVersion));
                UpdateCheckState = IsUpdateAvailable ? UpdateCheckState.Available : UpdateCheckState.Current;
                AppLogger.Info($"GitHub update check completed. Current: {CurrentVersion}; latest: {release.Tag}.");
            }
            catch (OperationCanceledException) when (_isDisposed) { }
            catch (Exception ex)
            {
                if (_isDisposed) return;
                UpdateCheckState = ex is System.Net.Http.HttpRequestException
                    { StatusCode: System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.TooManyRequests }
                    ? UpdateCheckState.RateLimited : UpdateCheckState.Failed;
                AppLogger.Error("GitHub update check failed.", ex);
            }
        }

        [RelayCommand]
        private void OpenUpdate()
        {
            if (!IsUpdateAvailable || _latestRelease == null) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = (_latestRelease.DownloadUri ?? _latestRelease.PageUri).AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                UpdateCheckState = UpdateCheckState.Failed;
                AppLogger.Error("Failed to open the GitHub update download.", ex);
            }
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

            ShowSelectedServer();
        }

        private void ShowSelectedServer()
        {
            Users.Clear();
            _currentConnectionId = SelectedServer?.ConnectionId ?? 0;
            _currentChannelId = SelectedServer?.MyChannelId ?? "";

            if (SelectedServer == null)
            {
                if (_manualRefreshCancellation == null)
                    ConnectionStatus = LanguageHelper.GetString("Lang_Status_Waiting");
                return;
            }

            if (_manualRefreshCancellation == null) ConnectionStatus = "";
            foreach (User user in SelectedServer.Users
                .Where(user => user.ChannelId == _currentChannelId)
                .OrderBy(user => user.Name))
            {
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

        // Refresh has its own lifetime; cache publications are not acknowledgements.
        public void RefreshData()
        {
            if (_isDisposed) return;
            CancelManualRefresh();
            if (!IsConnected)
            {
                ConnectionStatus = LanguageHelper.GetString("Lang_Status_RemoteUnavailable");
                return;
            }

            ConnectionStatus = LanguageHelper.GetString("Lang_Status_Refreshing");
            var cancellation = new CancellationTokenSource();
            _manualRefreshCancellation = cancellation;
            _ = RefreshWithTimeoutAsync(cancellation);
        }

        private async Task RefreshWithTimeoutAsync(CancellationTokenSource cancellation)
        {
            try
            {
                AppLogger.Info("Manual TS6 refresh requested.");
                if (_tsService.SendAuth())
                {
                    await Task.Delay(3000, cancellation.Token);
                    AppLogger.Info("TS6 refresh has not replied; retrying once.");
                    if (_tsService.SendAuth())
                        await Task.Delay(3000, cancellation.Token);
                }

                if (!ReferenceEquals(_manualRefreshCancellation, cancellation) || _isDisposed) return;
                _manualRefreshCancellation = null;
                ConnectionStatus = LanguageHelper.GetString("Lang_Status_RefreshTimeout");
                AppLogger.Warning("TS6 refresh failed or timed out. Check Remote App authorization and the configured port.");
            }
            catch (OperationCanceledException)
            {
                // A snapshot, disconnect, newer refresh, or shutdown ended this request.
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        private void CancelManualRefresh()
        {
            CancellationTokenSource? previous = _manualRefreshCancellation;
            _manualRefreshCancellation = null;
            previous?.Cancel();
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

        partial void OnIsOverlayLockedChanged(bool value)
        {
            Config.IsLocked = value;
        }
        
        public void SaveConfig() => ConfigService.Save(Config);

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            LanguageHelper.LanguageChanged -= OnLanguageChanged;
            _updateLifetime.Cancel();
            _updateLifetime.Dispose();
            CancelManualRefresh();
            _tsService.Dispose();
        }
    }
}
