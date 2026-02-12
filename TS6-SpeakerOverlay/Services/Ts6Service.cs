using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Websocket.Client;
using TS6_SpeakerOverlay.Models;
using System.Collections.Generic;

namespace TS6_SpeakerOverlay.Services
{
    public class Ts6Service
    {
        private const string URL = "ws://127.0.0.1:5899"; 
        
        // --- [核心修改] 定义 AppData 存储路径 ---
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "TS6-SpeakerOverlay"
        );
        private static readonly string KEY_FILE = Path.Combine(AppDataFolder, "apikey.txt");
        // --------------------------------------

        private WebsocketClient _client;
        private string _savedApiKey = "";
        private DateTime _lastRefreshTime = DateTime.MinValue;

        public event Action<List<User>, string>? OnChannelListUpdated;
        public event Action<int, bool>? OnTalkStatusChanged;
        public event Action<int, bool?, bool?, bool?>? OnUserPropertiesChanged;
        public event Action<int, string, string>? OnClientMoved;
        public event Action<bool>? OnConnectionStateChanged;

        public Ts6Service()
        {
            LoadApiKey(); 
            var factory = new Func<ClientWebSocket>(() => new ClientWebSocket());
            _client = new WebsocketClient(new Uri(URL), factory);
            
            _client.ReconnectTimeout = TimeSpan.FromSeconds(5); 
            _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(5);

            _client.ReconnectionHappened.Subscribe(info => 
            {
                OnConnectionStateChanged?.Invoke(true); 
                SendAuth();
            });

            _client.DisconnectionHappened.Subscribe(info => 
            {
                OnConnectionStateChanged?.Invoke(false);
            });

            _client.MessageReceived.Subscribe(msg => HandleMessage(msg.Text));
        }

        public async Task StartAsync() => await _client.Start();

        private void LoadApiKey()
        {
            // 确保 AppData 文件夹存在
            if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);

            if (File.Exists(KEY_FILE))
            {
                _savedApiKey = File.ReadAllText(KEY_FILE).Trim();
            }
        }

        public void SendAuth()
        {
            var auth = new AuthRequest();
            if (!string.IsNullOrEmpty(_savedApiKey)) auth.Payload.Content.ApiKey = _savedApiKey;
            _client.Send(JsonSerializer.Serialize(auth));
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
            catch { }
        }

        private void HandleAuthResponse(JsonNode? node)
        {
            var payload = node?["payload"];
            if (payload == null) return;

            var newKey = payload["apiKey"]?.ToString();
            if (!string.IsNullOrEmpty(newKey) && newKey != _savedApiKey)
            {
                _savedApiKey = newKey;
                // 确保文件夹存在后再保存
                if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                File.WriteAllText(KEY_FILE, newKey);
            }

            var connections = payload["connections"]?.AsArray();
            if (connections == null || connections.Count == 0) 
            {
                OnChannelListUpdated?.Invoke(new List<User>(), ""); 
                return;
            }

            var conn = connections.FirstOrDefault();
            if (conn == null) return;

            int myClientId = conn["clientId"]?.GetValue<int>() ?? 0;
            var clientInfos = conn["clientInfos"]?.AsArray();
            
            if (clientInfos == null) 
            {
                OnChannelListUpdated?.Invoke(new List<User>(), "");
                return;
            }

            var allUsers = new List<User>();
            string myChannelId = "";

            foreach (var client in clientInfos)
            {
                int id = client["id"]?.GetValue<int>() ?? 0;
                var props = client["properties"];
                string chId = client["channelId"]?.ToString() ?? "";

                if (id == myClientId) myChannelId = chId;

                string avatarRaw = props?["myteamspeakAvatar"]?.ToString() ?? "";
                string avatarUrl = "";
                if (!string.IsNullOrEmpty(avatarRaw) && avatarRaw.Contains(','))
                {
                    var parts = avatarRaw.Split(',');
                    if (parts.Length > 1) avatarUrl = parts[1];
                }

                allUsers.Add(new User 
                { 
                    ClientId = id,
                    Name = props?["nickname"]?.ToString() ?? "Unknown",
                    ChannelId = chId, 
                    AvatarUrl = avatarUrl,
                    IsTalking = props?["flagTalking"]?.GetValue<bool>() ?? false,
                    IsInputMuted = props?["inputMuted"]?.GetValue<bool>() ?? false,
                    IsOutputMuted = props?["outputMuted"]?.GetValue<bool>() ?? false,
                    IsAway = props?["away"]?.GetValue<bool>() ?? false
                });
            }
            OnChannelListUpdated?.Invoke(allUsers, myChannelId);
        }

        private void HandleClientMoved(JsonNode? node)
        {
            var payload = node?["payload"];
            if (payload == null) return;

            int clientId = payload["clientId"]?.GetValue<int>() ?? 0;
            string newCh = payload["newChannelId"]?.ToString() ?? "";
            string oldCh = payload["oldChannelId"]?.ToString() ?? "";

            OnClientMoved?.Invoke(clientId, newCh, oldCh);

            if ((DateTime.Now - _lastRefreshTime).TotalSeconds > 0.2)
            {
                _lastRefreshTime = DateTime.Now;
                SendAuth();
            }
        }

        private void HandleTalkStatus(JsonNode? node)
        {
            var payload = node?["payload"];
            int clientId = payload?["clientId"]?.GetValue<int>() ?? 0;
            int status = payload?["status"]?.GetValue<int>() ?? 0;
            OnTalkStatusChanged?.Invoke(clientId, status == 1);
        }

        private void HandlePropertiesUpdated(JsonNode? node)
        {
            var payload = node?["payload"];
            int clientId = payload?["clientId"]?.GetValue<int>() ?? 0;
            var props = payload?["properties"];

            if (props != null && clientId != 0)
            {
                bool? inputMuted = null;
                if (props["inputMuted"] != null) inputMuted = props["inputMuted"].GetValue<bool>();
                bool? outputMuted = null;
                if (props["outputMuted"] != null) outputMuted = props["outputMuted"].GetValue<bool>();
                bool? away = null;
                if (props["away"] != null) away = props["away"].GetValue<bool>();

                if (inputMuted.HasValue || outputMuted.HasValue || away.HasValue)
                {
                    OnUserPropertiesChanged?.Invoke(clientId, inputMuted, outputMuted, away);
                }
            }
        }
    }
}