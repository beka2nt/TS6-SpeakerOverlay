using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TS6_SpeakerOverlay.Services;
using Websocket.Client;

internal static class ConnectionLifecycleChecks
{
    private static int _checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS: " + name);
    }

    internal static int Run() => Task.Run(RunAsync).GetAwaiter().GetResult();

    private static async Task<int> RunAsync()
    {
        await using var server = new SilentRemoteApp();
        using var service = new Ts6Service(server.Port, false);
        var client = (WebsocketClient)typeof(Ts6Service).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service)!;
        int snapshots = 0, disconnects = 0;
        service.OnSnapshotReceived += () => Interlocked.Increment(ref snapshots);
        service.OnConnectionStateChanged += connected => { if (!connected) Interlocked.Increment(ref disconnects); };
        Check(client.ReconnectTimeout == null, "application-message silence cannot trigger a reconnect");
        Check(client.ErrorReconnectTimeout == TimeSpan.FromSeconds(5) && client.LostReconnectTimeout == TimeSpan.FromSeconds(5),
            "real connection failures retain bounded automatic retries");
        await service.StartAsync();
        await WaitUntil(() => Volatile.Read(ref snapshots) == 1, TimeSpan.FromSeconds(5));
        Check(client.NativeClient!.Options.KeepAliveInterval == TimeSpan.FromSeconds(20)
            && client.NativeClient.Options.KeepAliveTimeout == TimeSpan.FromSeconds(10), "native transport uses PING/PONG heartbeat instead of event silence");
        // Cross the old 5s timeout and a complete 20s PING / 10s PONG window.
        await Task.Delay(TimeSpan.FromSeconds(32));
        Check(server.ConnectionCount == 1 && Volatile.Read(ref disconnects) == 0 && client.IsRunning,
            "healthy silent local connection remains connected through heartbeat window");
        await server.CloseFirstPeer();
        await WaitUntil(() => Volatile.Read(ref snapshots) >= 2, TimeSpan.FromSeconds(15));
        Check(server.ConnectionCount >= 2 && Volatile.Read(ref disconnects) >= 1 && client.IsRunning,
            "actual server close reconnects and requests a fresh snapshot");
        int snapshotsBeforeRejection = Volatile.Read(ref snapshots);
        typeof(Ts6Service).GetMethod("HandleMessage", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(service,
            ["{\"type\":\"auth\",\"status\":{\"code\":403},\"payload\":{\"connections\":[]}}"]);
        Check(Volatile.Read(ref snapshots) == snapshotsBeforeRejection, "rejected authentication is not accepted as a valid empty snapshot");
        service.Dispose();
        int countAtDispose = server.ConnectionCount;
        await Task.Delay(5500);
        Check(server.ConnectionCount == countAtDispose && !service.SendAuth(), "shutdown stops reconnection and authentication sends");
        return _checks;
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        while (!condition()) await Task.Delay(25, deadline.Token);
    }

    private sealed class SilentRemoteApp : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly ConcurrentBag<Task> _handlers = new();
        private readonly ConcurrentQueue<WebSocket> _peers = new();
        private readonly Task _accept;
        private int _connections;
        internal int ConnectionCount => Volatile.Read(ref _connections);
        internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        internal SilentRemoteApp()
        {
            _listener.Start();
            _accept = Accept();
        }

        private async Task Accept()
        {
            try
            {
                while (!_lifetime.IsCancellationRequested)
                {
                    TcpClient peer = await _listener.AcceptTcpClientAsync(_lifetime.Token);
                    Interlocked.Increment(ref _connections);
                    _handlers.Add(Serve(peer));
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task Serve(TcpClient peer)
        {
            using (peer)
            {
                try
                {
                    var stream = peer.GetStream();
                    var header = new List<byte>();
                    byte[] one = new byte[1];
                    while (header.Count < 16384)
                    {
                        if (await stream.ReadAsync(one, _lifetime.Token) == 0) return;
                        header.Add(one[0]);
                        if (header.Count >= 4 && header.TakeLast(4).SequenceEqual(new byte[] {13,10,13,10})) break;
                    }
                    string key = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n")
                        .Single(line => line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)).Split(':', 2)[1].Trim();
                    string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n"), _lifetime.Token);
                    using var socket = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromMinutes(1));
                    _peers.Enqueue(socket);
                    byte[] buffer = new byte[65536];
                    while (!_lifetime.IsCancellationRequested)
                    {
                        var message = await socket.ReceiveAsync(buffer.AsMemory(), _lifetime.Token);
                        if (message.MessageType == WebSocketMessageType.Close)
                        {
                            if (socket.State == WebSocketState.CloseReceived)
                                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Fixture acknowledgment", _lifetime.Token);
                            break;
                        }
                        // The production service sends auth. Reply without any
                        // apiKey: tests must never write a user's key file.
                        if (message.EndOfMessage && message.MessageType == WebSocketMessageType.Text)
                            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"auth\",\"status\":{\"code\":0},\"payload\":{\"connections\":[]}}"),
                                WebSocketMessageType.Text, true, _lifetime.Token);
                    }
                }
                catch (Exception error) when (error is OperationCanceledException or IOException or WebSocketException) { }
            }
        }

        internal async Task CloseFirstPeer()
        {
            if (!_peers.TryDequeue(out var peer)) throw new InvalidOperationException("Fixture peer not available.");
            await peer.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "Fixture restart", _lifetime.Token);
        }

        public async ValueTask DisposeAsync()
        {
            _lifetime.Cancel();
            await _accept;
            await Task.WhenAll(_handlers);
            _listener.Stop();
            _lifetime.Dispose();
        }
    }
}
