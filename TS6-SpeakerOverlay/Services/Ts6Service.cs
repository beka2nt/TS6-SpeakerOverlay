using System;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive.Disposables;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;
using TS6_SpeakerOverlay.Models;
using System.Collections.Generic;

namespace TS6_SpeakerOverlay.Services
{
    public sealed class Ts6Service : IDisposable
    {
        private const int DefaultPort = 5899;
        private readonly WebsocketClient _client;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly object _refreshSync = new();
        private readonly object _stateSync = new();
        private List<Ts6Server> _cachedServers = new();
        private readonly Dictionary<(int ConnectionId, int ClientId), DateTime> _pendingNewClients = new();
        private readonly Dictionary<(int ConnectionId, int ClientId), DateTime> _pendingDepartedClients = new();
        private bool _hasAuthoritativeSnapshot;
        private string _savedApiKey = "";
        private CancellationTokenSource? _refreshCancellation;
        private int _isDisposed;

        public event Action<List<Ts6Server>>? OnServersUpdated;
        public event Action<int, int, bool>? OnTalkStatusChanged;
        public event Action<int, int, bool?, bool?, bool?>? OnUserPropertiesChanged;
        public event Action<int, int, string, string, string?>? OnClientMoved;
        public event Action<int, int, string, bool>? OnServerMembershipChanged;
        public event Action<bool>? OnConnectionStateChanged;

        public Ts6Service(int port = DefaultPort)
        {
            LoadApiKey(); 
            var factory = new Func<ClientWebSocket>(() => new ClientWebSocket());
            int selectedPort = port is >= 1 and <= 65535 ? port : DefaultPort;
            AppLogger.Info($"Connecting to the local TS6 Remote App on port {selectedPort}.");
            _client = new WebsocketClient(new Uri($"ws://127.0.0.1:{selectedPort}"), factory);
            
            _client.ReconnectTimeout = TimeSpan.FromSeconds(5); 
            _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(5);

            _subscriptions.Add(_client.ReconnectionHappened.Subscribe(info =>
            {
                AppLogger.Info("TS6 WebSocket connected.");
                OnConnectionStateChanged?.Invoke(true); 
                SendAuth();
            }));

            _subscriptions.Add(_client.DisconnectionHappened.Subscribe(info =>
            {
                AppLogger.Warning("TS6 WebSocket disconnected; waiting to reconnect.");
                lock (_stateSync)
                {
                    _cachedServers.Clear();
                    _pendingNewClients.Clear();
                    _pendingDepartedClients.Clear();
                    _hasAuthoritativeSnapshot = false;
                }
                OnConnectionStateChanged?.Invoke(false);
            }));

            _subscriptions.Add(_client.MessageReceived.Subscribe(msg => HandleMessage(msg.Text)));
        }

        public async Task StartAsync()
        {
            try
            {
                await _client.Start();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to start the TS6 WebSocket client.", ex);
                OnConnectionStateChanged?.Invoke(false);
            }
        }

        private void LoadApiKey()
        {
            _savedApiKey = ApiKeyStore.Load();
        }

        public void SendAuth()
        {
            if (Volatile.Read(ref _isDisposed) != 0) return;

            try
            {
                var auth = new AuthRequest();
                auth.Payload.Version = typeof(Ts6Service).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
                if (!string.IsNullOrEmpty(_savedApiKey)) auth.Payload.Content.ApiKey = _savedApiKey;
                _client.Send(JsonSerializer.Serialize(auth));
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to send TS6 authentication.", ex);
            }
        }

        private void HandleMessage(string? json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try 
            {
                var node = JsonNode.Parse(json);
                string? type = node?["type"]?.ToString();

                switch (type)
                {
                    case "auth": HandleAuthResponse(node); break;
                    case "talkStatusChanged": HandleTalkStatus(node); break;
                    case "clientPropertiesUpdated": HandlePropertiesUpdated(node); break;
                    case "clientMoved": HandleClientMoved(node); break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to process a TS6 message.", ex);
            }
        }

        private void HandleAuthResponse(JsonNode? node)
        {
            var payload = node?["payload"];
            if (payload == null) return;

            var newKey = payload["apiKey"]?.ToString();
            if (!string.IsNullOrEmpty(newKey) && newKey != _savedApiKey)
            {
                _savedApiKey = newKey;
                ApiKeyStore.Save(newKey);
            }

            var connections = payload["connections"]?.AsArray();
            if (connections == null || connections.Count == 0) 
            {
                AppLogger.Info("TS6 is not connected to any server.");
                ReplaceCachedServers(new List<Ts6Server>());
                return;
            }

            var servers = new List<Ts6Server>();
            foreach (var connection in connections)
            {
                if (connection == null) continue;

                int connectionId = GetInt(connection["id"]);
                int myClientId = GetInt(connection["clientId"]);
                var serverProperties = connection["properties"];
                string uniqueIdentifier = serverProperties?["uniqueIdentifier"]?.ToString() ?? $"connection:{connectionId}";
                string name = serverProperties?["name"]?.ToString() ?? $"Server {connectionId}";
                var users = new List<User>();
                string myChannelId = "";

                if (connection["clientInfos"] is JsonArray clientInfos)
                {
                    foreach (var client in clientInfos)
                    {
                        if (client == null) continue;

                        int clientId = GetInt(client["id"]);
                        var clientProperties = client["properties"];
                        string channelId = client["channelId"]?.ToString() ?? "";
                        if (clientId == myClientId) myChannelId = channelId;

                        string clientUniqueIdentifier = clientProperties?["uniqueIdentifier"]?.ToString() ?? "";
                        string avatarHash = clientProperties?["flagAvatar"]?.ToString() ?? "";

                        users.Add(new User
                        {
                            ClientId = clientId,
                            Name = clientProperties?["nickname"]?.ToString() ?? "Unknown",
                            ChannelId = channelId,
                            ClientUniqueIdentifier = clientUniqueIdentifier,
                            ServerAvatarPath = Ts6AvatarCache.ResolveServerAvatarPath(
                                uniqueIdentifier,
                                clientUniqueIdentifier,
                                avatarHash),
                            IsTalking = GetBool(clientProperties?["flagTalking"]),
                            IsInputMuted = GetBool(clientProperties?["inputMuted"]),
                            IsOutputMuted = GetBool(clientProperties?["outputMuted"]),
                            IsAway = GetBool(clientProperties?["away"])
                        });
                    }
                }

                servers.Add(new Ts6Server
                {
                    ConnectionId = connectionId,
                    UniqueIdentifier = uniqueIdentifier,
                    Name = name,
                    MyClientId = myClientId,
                    MyChannelId = myChannelId,
                    Users = users
                });
            }

            ReplaceCachedServers(servers);
        }

        private void HandleClientMoved(JsonNode? node)
        {
            var payload = node?["payload"];
            if (payload == null) return;

            int clientId = GetInt(payload["clientId"]);
            string newCh = payload["newChannelId"]?.ToString() ?? "";
            string oldCh = payload["oldChannelId"]?.ToString() ?? "";
            bool oldChannelProvided = payload["oldChannelId"] != null;
            int connectionId = ResolveConnectionId(node, payload, clientId, oldCh, newCh);

            string? clientName;
            bool cacheChanged = false;
            bool newClient = false;
            bool? membershipJoined = null;
            lock (_stateSync)
            {
                Ts6Server? server = _cachedServers.FirstOrDefault(item => item.ConnectionId == connectionId);
                User? movedUser = server?.Users.FirstOrDefault(user => user.ClientId == clientId);
                clientName = movedUser?.Name ?? GetMovedClientName(payload);

                if (!oldChannelProvided && movedUser != null)
                    oldCh = movedUser.ChannelId;

                if (movedUser != null)
                {
                    if (string.IsNullOrEmpty(newCh) || newCh == "0")
                    {
                        server!.Users.Remove(movedUser);
                        _pendingDepartedClients[(connectionId, clientId)] = DateTime.UtcNow.AddSeconds(5);
                        membershipJoined = false;
                    }
                    else
                        movedUser.ChannelId = newCh;
                    cacheChanged = true;
                }
                else if (server != null && clientId != 0 && !string.IsNullOrEmpty(newCh) && newCh != "0")
                {
                    // A newly connected client is not present in the previous auth snapshot.
                    // Show them immediately, then replace the temporary details on refresh.
                    var clientProperties = payload["properties"] ?? payload["clientInfo"]?["properties"];
                    string clientUniqueIdentifier = clientProperties?["uniqueIdentifier"]?.ToString() ?? string.Empty;
                    movedUser = new User
                    {
                        ClientId = clientId,
                        ChannelId = newCh,
                        Name = clientName ?? $"成员 #{clientId}",
                        ClientUniqueIdentifier = clientUniqueIdentifier,
                        ServerAvatarPath = Ts6AvatarCache.ResolveServerAvatarPath(
                            server.UniqueIdentifier,
                            clientUniqueIdentifier,
                            clientProperties?["flagAvatar"]?.ToString() ?? string.Empty)
                    };
                    server.Users.Add(movedUser);
                    _pendingNewClients[(connectionId, clientId)] = DateTime.UtcNow.AddSeconds(3);
                    cacheChanged = true;
                    newClient = true;
                    if (string.IsNullOrEmpty(oldCh) || oldCh == "0") membershipJoined = true;
                }

                if (string.IsNullOrEmpty(newCh) || newCh == "0")
                {
                    _pendingNewClients.Remove((connectionId, clientId));
                }
                else
                {
                    _pendingDepartedClients.Remove((connectionId, clientId));
                }

                if (server != null && clientId == server.MyClientId)
                {
                    server.MyChannelId = newCh;
                    cacheChanged = true;
                }
            }

            if (cacheChanged) PublishServerSnapshots();

            if (membershipJoined.HasValue && connectionId != 0 && clientId != 0)
                OnServerMembershipChanged?.Invoke(connectionId, clientId,
                    clientName ?? $"成员 #{clientId}", membershipJoined.Value);

            if (connectionId != 0)
            {
                OnClientMoved?.Invoke(connectionId, clientId, newCh, oldCh, clientName);
            }

            ScheduleRefresh(newClient);
        }

        private void HandleTalkStatus(JsonNode? node)
        {
            var payload = node?["payload"];
            int clientId = GetInt(payload?["clientId"]);
            int connectionId = ResolveConnectionId(node, payload, clientId);
            int status = GetInt(payload?["status"]);
            bool isTalking = status == 1;

            lock (_stateSync)
            {
                User? user = _cachedServers
                    .FirstOrDefault(server => server.ConnectionId == connectionId)?
                    .Users.FirstOrDefault(item => item.ClientId == clientId);
                if (user != null) user.IsTalking = isTalking;
            }

            if (connectionId != 0) OnTalkStatusChanged?.Invoke(connectionId, clientId, isTalking);
        }

        private void HandlePropertiesUpdated(JsonNode? node)
        {
            var payload = node?["payload"];
            int clientId = GetInt(payload?["clientId"]);
            int connectionId = ResolveConnectionId(node, payload, clientId);
            var props = payload?["properties"];

            if (props != null && clientId != 0)
            {
                bool? inputMuted = null;
                if (props["inputMuted"] != null) inputMuted = props["inputMuted"]?.GetValue<bool>();
                bool? outputMuted = null;
                if (props["outputMuted"] != null) outputMuted = props["outputMuted"]?.GetValue<bool>();
                bool? away = null;
                if (props["away"] != null) away = props["away"]?.GetValue<bool>();
                bool avatarChanged = props["flagAvatar"] != null;

                if (inputMuted.HasValue || outputMuted.HasValue || away.HasValue || avatarChanged)
                {
                    lock (_stateSync)
                    {
                        Ts6Server? server = _cachedServers
                            .FirstOrDefault(item => item.ConnectionId == connectionId);
                        User? user = server?.Users.FirstOrDefault(item => item.ClientId == clientId);
                        if (user != null)
                        {
                            if (inputMuted.HasValue) user.IsInputMuted = inputMuted.Value;
                            if (outputMuted.HasValue) user.IsOutputMuted = outputMuted.Value;
                            if (away.HasValue) user.IsAway = away.Value;
                            if (avatarChanged && server != null)
                            {
                                user.ServerAvatarPath = Ts6AvatarCache.ResolveServerAvatarPath(
                                    server.UniqueIdentifier,
                                    user.ClientUniqueIdentifier,
                                    props["flagAvatar"]?.ToString() ?? "");
                            }
                        }
                    }

                    if (connectionId != 0)
                    {
                        OnUserPropertiesChanged?.Invoke(connectionId, clientId, inputMuted, outputMuted, away);
                    }

                    if (avatarChanged) PublishServerSnapshots();
                }
            }
        }

        private int ResolveConnectionId(JsonNode? node, JsonNode? payload, int clientId,
            string? previousChannelId = null, string? destinationChannelId = null)
        {
            int explicitId = GetInt(payload?["connectionId"]);
            if (explicitId == 0) explicitId = GetInt(payload?["serverConnectionHandlerId"]);
            if (explicitId == 0) explicitId = GetInt(payload?["connection"]);
            if (explicitId == 0) explicitId = GetInt(node?["connectionId"]);

            lock (_stateSync)
            {
                if (explicitId != 0 && _cachedServers.Any(server => server.ConnectionId == explicitId))
                {
                    return explicitId;
                }

                Ts6Server[] matches = _cachedServers
                    .Where(server => server.Users.Any(user => user.ClientId == clientId))
                    .ToArray();

                if (!string.IsNullOrEmpty(previousChannelId) && matches.Length > 1)
                {
                    Ts6Server[] channelMatches = matches
                        .Where(server => server.Users.Any(user =>
                            user.ClientId == clientId && user.ChannelId == previousChannelId))
                        .ToArray();
                    if (channelMatches.Length == 1) return channelMatches[0].ConnectionId;
                }

                if (matches.Length == 0)
                {
                    // The event may arrive before the new client appears in an auth snapshot.
                    if (_cachedServers.Count == 1) return _cachedServers[0].ConnectionId;

                    if (!string.IsNullOrEmpty(destinationChannelId))
                    {
                        Ts6Server[] destinationMatches = _cachedServers
                            .Where(server => server.MyChannelId == destinationChannelId)
                            .ToArray();
                        if (destinationMatches.Length == 1) return destinationMatches[0].ConnectionId;
                    }
                }

                return matches.Length == 1 ? matches[0].ConnectionId : 0;
            }
        }

        private static string? GetMovedClientName(JsonNode payload)
        {
            string? name = payload["clientName"]?.ToString()
                ?? payload["nickname"]?.ToString()
                ?? payload["clientInfo"]?["properties"]?["nickname"]?.ToString()
                ?? payload["properties"]?["nickname"]?.ToString();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private void ReplaceCachedServers(List<Ts6Server> servers)
        {
            var membershipChanges = new List<(int ConnectionId, int ClientId, string Name, bool Joined)>();
            lock (_stateSync)
            {
                foreach (var pending in _pendingNewClients.ToArray())
                {
                    if (pending.Value <= DateTime.UtcNow)
                    {
                        _pendingNewClients.Remove(pending.Key);
                        continue;
                    }

                    Ts6Server? refreshedServer = servers.FirstOrDefault(server =>
                        server.ConnectionId == pending.Key.ConnectionId);
                    if (refreshedServer == null) continue;

                    if (refreshedServer.Users.Any(user => user.ClientId == pending.Key.ClientId))
                    {
                        _pendingNewClients.Remove(pending.Key);
                        continue;
                    }

                    User? provisional = _cachedServers
                        .FirstOrDefault(server => server.ConnectionId == pending.Key.ConnectionId)?
                        .Users.FirstOrDefault(user => user.ClientId == pending.Key.ClientId);
                    if (provisional != null) refreshedServer.Users.Add(CloneUser(provisional));
                }

                foreach (var pending in _pendingDepartedClients.ToArray())
                {
                    if (pending.Value <= DateTime.UtcNow)
                    {
                        _pendingDepartedClients.Remove(pending.Key);
                        continue;
                    }

                    Ts6Server? refreshedServer = servers.FirstOrDefault(server =>
                        server.ConnectionId == pending.Key.ConnectionId);
                    User? staleUser = refreshedServer?.Users.FirstOrDefault(user =>
                        user.ClientId == pending.Key.ClientId);
                    if (staleUser == null)
                    {
                        _pendingDepartedClients.Remove(pending.Key);
                        continue;
                    }

                    refreshedServer!.Users.Remove(staleUser);
                }

                if (_hasAuthoritativeSnapshot)
                {
                    foreach (Ts6Server previous in _cachedServers)
                    {
                        Ts6Server? refreshed = servers.FirstOrDefault(server =>
                            server.ConnectionId == previous.ConnectionId);
                        if (refreshed == null) continue;

                        foreach (User joined in refreshed.Users.Where(user =>
                            previous.Users.All(oldUser => oldUser.ClientId != user.ClientId)))
                        {
                            membershipChanges.Add((refreshed.ConnectionId, joined.ClientId, joined.Name, true));
                        }

                        foreach (User departed in previous.Users.Where(user =>
                            refreshed.Users.All(newUser => newUser.ClientId != user.ClientId)))
                        {
                            membershipChanges.Add((previous.ConnectionId, departed.ClientId, departed.Name, false));
                        }
                    }
                }

                if (servers.Count == 0)
                {
                    _pendingNewClients.Clear();
                    _pendingDepartedClients.Clear();
                }
                _cachedServers = servers.Select(CloneServer).ToList();
                _hasAuthoritativeSnapshot = true;
            }

            PublishServerSnapshots();
            foreach (var change in membershipChanges)
                OnServerMembershipChanged?.Invoke(change.ConnectionId, change.ClientId,
                    change.Name, change.Joined);
        }

        private void PublishServerSnapshots()
        {
            List<Ts6Server> snapshot;
            lock (_stateSync)
            {
                snapshot = _cachedServers.Select(CloneServer).ToList();
            }

            OnServersUpdated?.Invoke(snapshot);
        }

        private static Ts6Server CloneServer(Ts6Server server) => new()
        {
            ConnectionId = server.ConnectionId,
            UniqueIdentifier = server.UniqueIdentifier,
            Name = server.Name,
            MyClientId = server.MyClientId,
            MyChannelId = server.MyChannelId,
            Users = server.Users.Select(CloneUser).ToList()
        };

        private static User CloneUser(User user) => new()
        {
            ClientId = user.ClientId,
            Name = user.Name,
            ChannelId = user.ChannelId,
            ClientUniqueIdentifier = user.ClientUniqueIdentifier,
            ServerAvatarPath = user.ServerAvatarPath,
            IsTalking = user.IsTalking,
            IsInputMuted = user.IsInputMuted,
            IsOutputMuted = user.IsOutputMuted,
            IsAway = user.IsAway,
            LastTalkTime = user.LastTalkTime
        };

        private static int GetInt(JsonNode? value)
        {
            return int.TryParse(value?.ToString(), out int result) ? result : 0;
        }

        private static bool GetBool(JsonNode? value)
        {
            return bool.TryParse(value?.ToString(), out bool result) && result;
        }

        private void ScheduleRefresh(bool retryForNewClient = false)
        {
            if (Volatile.Read(ref _isDisposed) != 0) return;

            var current = new CancellationTokenSource();
            CancellationTokenSource? previous;
            lock (_refreshSync)
            {
                previous = _refreshCancellation;
                _refreshCancellation = current;
                previous?.Cancel();
            }

            _ = RefreshAfterDelayAsync(current, retryForNewClient);
        }

        private async Task RefreshAfterDelayAsync(CancellationTokenSource cancellation, bool retryForNewClient)
        {
            try
            {
                int[] delays = retryForNewClient ? [50, 350, 900, 2000] : [50];
                foreach (int delay in delays)
                {
                    await Task.Delay(delay, cancellation.Token);
                    SendAuth();
                }
            }
            catch (OperationCanceledException)
            {
                // A newer movement event replaced this refresh.
            }
            finally
            {
                lock (_refreshSync)
                {
                    if (ReferenceEquals(_refreshCancellation, cancellation))
                    {
                        _refreshCancellation = null;
                    }
                }

                cancellation.Dispose();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

            CancellationTokenSource? refreshCancellation;
            lock (_refreshSync)
            {
                refreshCancellation = _refreshCancellation;
                _refreshCancellation = null;
                refreshCancellation?.Cancel();
            }

            _subscriptions.Dispose();
            _client.Dispose();
        }
    }
}
