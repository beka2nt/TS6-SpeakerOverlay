using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Services;

internal static class AutoPortConnectionChecks
{
    private static int _checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS: " + name);
    }

    internal static int Run() => Task.Run(RunAsync).GetAwaiter().GetResult();

    private static async Task<int> RunAsync()
    {
        var a = new Ts6Endpoint("127.0.0.1", 6200, 100);
        var b = new Ts6Endpoint("127.0.0.1", 7300, 100);
        var c = new Ts6Endpoint("127.0.0.1", 7400, 100);
        var discovery = new Discovery(new(true, true, [a, b]));
        var made = new List<(Ts6Endpoint Endpoint, Connection Connection)>();
        ITs6Connection Factory(Ts6Endpoint endpoint)
        {
            var connection = new Connection();
            made.Add((endpoint, connection));
            return connection;
        }
        using (var manager = new Ts6ConnectionManager(discovery, Factory, true, 6200, TimeSpan.FromHours(1)))
        {
            int verified = 0, snapshots = 0, forwarded = 0, traffic = 0;
            manager.OnEndpointVerified += _ => verified++;
            manager.OnSnapshotReceived += () => snapshots++;
            manager.OnServersUpdated += _ => forwarded++;
            manager.OnTalkStatusChanged += (_, _, _) => traffic++;
            manager.OnUserPropertiesChanged += (_, _, _, _, _) => traffic++;
            manager.OnClientMoved += (_, _, _, _, _) => traffic++;
            manager.OnServerMembershipChanged += (_, _, _, _) => traffic++;
            await manager.StartAsync();
            Check(made.Count == 1 && made[0].Endpoint == a && manager.ActivePort == 6200,
                "automatic connection prioritizes the remembered trusted listener");
            Check(verified == 1 && snapshots == 1, "only the current valid snapshot verifies and publishes an endpoint");
            await manager.DetectNowAsync();
            Check(made.Count == 1 && made[0].Connection.AuthAttempts == 1, "re-detection does not churn a healthy unchanged session");
            Check(manager.State == Ts6PortState.Connected, "re-detecting a healthy session retains its connected status");
            discovery.Result = new(true, true, [b]);
            made[0].Connection.Drop();
            await manager.DetectNowAsync();
            Check(made.Count == 2 && made[0].Connection.Disposed && made[1].Endpoint == b && manager.ActivePort == 7300,
                "port migration disposes the old session and connects to the new listener");
            made[1].Connection.RaiseTraffic();
            Check(traffic == 4, "current-session membership and speaking events are forwarded");
            int beforeOld = snapshots + forwarded + traffic;
            made[0].Connection.PublishSnapshot();
            made[0].Connection.RaiseTraffic();
            Check(snapshots + forwarded + traffic == beforeOld, "late data from the retired session is discarded");
            await manager.ConfigureAsync(false, 7400);
            Check(made.Last().Endpoint.Port == 7400 && made.Last().Endpoint.Host == "127.0.0.1",
                "manual port change reconnects immediately without ownership discovery");
            Check(verified == 3, "successful endpoint is published once per session, not repeatedly");
        }

        async Task NoConnect(Ts6DiscoveryResult result, Ts6PortState expected, string name)
        {
            int connections = 0;
            using var manager = new Ts6ConnectionManager(new Discovery(result), _ => { connections++; return new Connection(); }, true, 6200);
            Ts6PortState? state = null;
            manager.OnPortStateChanged += value => state = value;
            await manager.StartAsync();
            Check(connections == 0 && state == expected && manager.ActivePort == null, name);
        }
        await NoConnect(new(false, true, []), Ts6PortState.WaitingForTeamSpeak, "stopped TS does not cause blind connection attempts");
        await NoConnect(new(true, true, []), Ts6PortState.NoRemoteAppListener, "missing Remote Apps listener has a distinct state");
        await NoConnect(new(true, false, []), Ts6PortState.InspectionFailed, "inspection failure cannot fall back to arbitrary ports");
        await NoConnect(new(true, true, [b, c]), Ts6PortState.AmbiguousCandidates, "multiple unverified candidates never receive an auth sweep");
        await NoConnect(new(true, true, [new("198.51.100.1", 6200, 100)]), Ts6PortState.InspectionFailed,
            "invalid non-loopback discovery output cannot create a session");

        var wrongOwner = new Discovery(new(true, true, [a])) { Trusted = false };
        int wrongConnections = 0;
        using (var manager = new Ts6ConnectionManager(wrongOwner, _ => { wrongConnections++; return new Connection(); }, true, 6200))
        {
            await manager.StartAsync();
            Check(wrongConnections == 0, "ownership change before connect prevents creating a credential-bearing session");
        }

        var changingOwner = new Discovery(new(true, true, [a]));
        var guardedPeer = new Connection { BeforeConnect = () => changingOwner.Trusted = false };
        using (var manager = new Ts6ConnectionManager(changingOwner, _ => guardedPeer, true, 6200))
        {
            await manager.StartAsync();
            Check(guardedPeer.Disposed && guardedPeer.AuthAttempts == 0 && manager.ActivePort == null,
                "ownership change during handshake disposes the session before authentication");
        }

        var pending = new Connection { AutoSnapshot = false };
        using (var manager = new Ts6ConnectionManager(new Discovery(new(true, true, [a])), _ => pending, true, 6200))
        {
            int verified = 0;
            manager.OnEndpointVerified += _ => verified++;
            await manager.StartAsync();
            await manager.DetectNowAsync();
            Check(verified == 0 && pending.Starts == 1 && pending.AuthAttempts == 1 && !pending.Disposed,
                "authorization-pending healthy connection is retained without repeated authentication");
            pending.PublishSnapshot();
            pending.PublishSnapshot();
            Check(verified == 1, "handshake alone cannot persist a port and repeated snapshots do not re-verify");
        }

        int rejections = 0;
        using (var manager = new Ts6ConnectionManager(new Discovery(new(true, true, [a])), _ =>
        { rejections++; return new Connection { RejectCode = 403 }; }, true, 6200, TimeSpan.FromMilliseconds(30)))
        {
            await manager.StartAsync();
            await Task.Delay(150);
            Check(rejections == 1 && manager.ActivePort == null, "rejected authorization pauses timer-driven authentication retries");
            await manager.DetectNowAsync();
            Check(rejections == 2, "explicit retry can attempt authorization again");
        }

        var dual = new Discovery(new(true, true, [new("127.0.0.1", 7300, 100), new("::1", 7300, 100)]));
        Ts6Endpoint? dualSelected = null;
        using (var manager = new Ts6ConnectionManager(dual, endpoint => { dualSelected = endpoint; return new Connection(); }, true, 6200))
        {
            await manager.StartAsync();
            Check(dualSelected?.Port == 7300 && dualSelected.Host == "127.0.0.1", "dual-stack listeners for one TS process and port are not falsely ambiguous");
        }

        var blocked = new TaskCompletionSource<Ts6DiscoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ipv6 = new Ts6Endpoint("::1", 7300, 100);
        var manualEndpoints = new List<Ts6Endpoint>();
        using (var manager = new Ts6ConnectionManager(new Discovery(new(true, true, [ipv6])), endpoint =>
        { manualEndpoints.Add(endpoint); return new Connection(); }, true, 6200))
        {
            await manager.StartAsync();
            await manager.ConfigureAsync(false, 7300);
            Check(manualEndpoints.Count == 2 && manualEndpoints.All(endpoint => endpoint == ipv6),
                "switching a verified IPv6 session to manual preserves the reachable TS address");
        }
        manualEndpoints.Clear();
        using (var manager = new Ts6ConnectionManager(new Discovery(new(true, true, [ipv6, c])), endpoint =>
        { manualEndpoints.Add(endpoint); return new Connection(); }, true, 6200))
        {
            await manager.StartAsync();
            Check(manager.State == Ts6PortState.AmbiguousCandidates, "IPv6 manual fallback starts from an explicit ambiguous-candidate state");
            await manager.ConfigureAsync(false, 7300);
            Check(manualEndpoints.SequenceEqual([ipv6]), "manual port selection resolves the TS-owned IPv6 listener after ambiguity");
        }
        manualEndpoints.Clear();
        using (var manager = new Ts6ConnectionManager(new Discovery(new(true, false, [])), endpoint =>
        { manualEndpoints.Add(endpoint); return new Connection(); }, false, 6200))
        {
            await manager.StartAsync();
            Check(manualEndpoints.Single() == new Ts6Endpoint("127.0.0.1", 6200, 0),
                "explicit manual IPv4 mode remains usable when listener inspection is unavailable");
        }
        var manualOwner = new Discovery(new(true, true, [ipv6]));
        var manualPeer = new Connection { BeforeConnect = () => manualOwner.Trusted = false };
        using (var manager = new Ts6ConnectionManager(manualOwner, _ => manualPeer, false, 7300))
        {
            await manager.StartAsync();
            Check(manualPeer.Disposed && manualPeer.AuthAttempts == 0,
                "a resolved manual TS listener is rechecked before authentication");
        }
        var slow = new Discovery(new(true, true, [a])) { Discover = _ => blocked.Task };
        var requested = new List<int>();
        using (var manager = new Ts6ConnectionManager(slow, endpoint => { requested.Add(endpoint.Port); return new Connection(); }, true, 6200))
        {
            var starting = manager.StartAsync();
            await Until(() => slow.Calls == 1);
            var switched = manager.ConfigureAsync(false, 7400);
            blocked.SetResult(new(true, true, [a]));
            await Task.WhenAll(starting, switched);
            Check(requested.SequenceEqual([7400]), "manual mode change invalidates an in-flight automatic discovery result");
        }

        var delayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowPeer = new Connection { StartWait = delayed.Task };
        var disposable = new Ts6ConnectionManager(new Discovery(new(true, true, [a])), _ => slowPeer, true, 6200);
        int afterExit = 0;
        disposable.OnEndpointVerified += _ => afterExit++;
        var startDuringExit = disposable.StartAsync();
        await Until(() => slowPeer.Starts == 1);
        disposable.Dispose();
        delayed.SetResult();
        await startDuringExit;
        slowPeer.PublishSnapshot();
        Check(slowPeer.Disposed && afterExit == 0 && !disposable.SendAuth(), "disposal during startup suppresses all late state and authentication");

        var timerDiscovery = new Discovery(new(true, true, [a]));
        var timerPeers = new System.Collections.Concurrent.ConcurrentQueue<Connection>();
        using (var manager = new Ts6ConnectionManager(timerDiscovery, _ => { var peer = new Connection(); timerPeers.Enqueue(peer); return peer; }, true, 6200, TimeSpan.FromMilliseconds(30)))
        {
            await manager.StartAsync();
            timerPeers.First().Drop();
            await Until(() => timerPeers.Count >= 2);
            Check(timerPeers.Count == 2 && timerPeers.Last().AuthAttempts == 1, "a lost unchanged endpoint is retried by the detection timer");
            int calls = timerDiscovery.Calls;
            await Task.Delay(150);
            Check(timerPeers.Count == 2 && timerDiscovery.Calls == calls, "healthy sessions stop periodic endpoint inspection");
        }
        var rapidStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rapidPeers = new List<(int Port, Connection Peer)>();
        var saved = new List<int>();
        using (var manager = new Ts6ConnectionManager(new Discovery(new(true, true, [a])), endpoint =>
        {
            var peer = new Connection { StartWait = endpoint.Port == 6200 ? rapidStart.Task : null };
            rapidPeers.Add((endpoint.Port, peer));
            return peer;
        }, true, 6200, TimeSpan.FromHours(1)))
        {
            manager.OnEndpointVerified += endpoint => saved.Add(endpoint.Port);
            var first = manager.StartAsync();
            await Until(() => rapidPeers.Count == 1);
            var second = manager.ConfigureAsync(false, 7300);
            var third = manager.ConfigureAsync(false, 7400);
            rapidStart.SetResult();
            await Task.WhenAll(first, second, third);
            rapidPeers[0].Peer.PublishSnapshot();
            Check(rapidPeers.Select(item => item.Port).SequenceEqual([6200, 7400]) && saved.SequenceEqual([7400]),
                "rapid A-B-C changes discard superseded startup and save only C");
        }
        using var queued = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        bool hold = false;
        int heldThread = -1, deliveredSnapshots = 0;
        var dispatchPeers = new List<Connection>();
        using (var manager = new Ts6ConnectionManager(new Discovery(new(true, true, [a])), _ =>
            { var peer = new Connection(); dispatchPeers.Add(peer); return peer; }, true, 6200, TimeSpan.FromHours(1), action =>
            {
                if (hold && Environment.CurrentManagedThreadId == Volatile.Read(ref heldThread))
                {
                    queued.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Fixture dispatch was not released.");
                }
                action();
            }))
        {
            manager.OnSnapshotReceived += () => deliveredSnapshots++;
            await manager.StartAsync();
            hold = true;
            var late = Task.Run(() => { Volatile.Write(ref heldThread, Environment.CurrentManagedThreadId); dispatchPeers[0].PublishSnapshot(); });
            try
            {
                await Until(() => queued.IsSet);
                await manager.ConfigureAsync(false, 7400);
                int beforeDelivery = deliveredSnapshots;
                hold = false;
                release.Set();
                await late;
                Check(deliveredSnapshots == beforeDelivery && manager.ActivePort == 7400,
                    "session generation is checked after queued UI delivery, not before queueing");
            }
            finally { hold = false; release.Set(); }
        }
        return _checks;
    }

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, timeout.Token);
    }

    internal sealed class Discovery(Ts6DiscoveryResult result) : ITs6PortDiscovery
    {
        internal Ts6DiscoveryResult Result = result;
        internal bool Trusted = true;
        internal int Calls;
        internal Func<CancellationToken, Task<Ts6DiscoveryResult>>? Discover;
        public Task<Ts6DiscoveryResult> DiscoverAsync(CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            return Discover?.Invoke(token) ?? Task.FromResult(Result);
        }
        public bool IsOwnedByTeamSpeak(Ts6Endpoint endpoint) => Trusted && Result.Candidates.Contains(endpoint);
    }

    internal sealed class Connection : ITs6Connection, ISessionGuardedTs6Connection
    {
        public event Action<List<Ts6Server>>? OnServersUpdated;
        public event Action? OnSnapshotReceived;
        public event Action<int, int, bool>? OnTalkStatusChanged;
        public event Action<int, int, bool?, bool?, bool?>? OnUserPropertiesChanged;
        public event Action<int, int, string, string, string?>? OnClientMoved;
        public event Action<int, int, string, bool>? OnServerMembershipChanged;
        public event Action<bool>? OnConnectionStateChanged;
        public event Action<int>? OnAuthenticationRejected;
        internal bool AutoSnapshot = true, Disposed;
        internal int RejectCode, AuthAttempts, Starts;
        internal Action? BeforeConnect;
        internal Task? StartWait;
        internal Ts6Service? GuardedService;
        public void SetSessionGuard(Action<Action> dispatchIfCurrent) => ((ISessionGuardedTs6Connection?)GuardedService)?.SetSessionGuard(dispatchIfCurrent);
        public async Task StartAsync()
        {
            Starts++;
            if (StartWait != null) await StartWait;
            if (Disposed) return;
            BeforeConnect?.Invoke();
            OnConnectionStateChanged?.Invoke(true);
            if (!SendAuth()) return;
            if (RejectCode != 0) OnAuthenticationRejected?.Invoke(RejectCode);
            else if (AutoSnapshot) PublishSnapshot();
        }
        public bool SendAuth() { if (Disposed) return false; AuthAttempts++; return true; }
        internal void PublishSnapshot() { OnServersUpdated?.Invoke([new() { Name = "Fixture" }]); OnSnapshotReceived?.Invoke(); }
        internal void Drop() => OnConnectionStateChanged?.Invoke(false);
        internal void RaiseTraffic()
        {
            OnTalkStatusChanged?.Invoke(1, 1, true);
            OnUserPropertiesChanged?.Invoke(1, 1, true, false, false);
            OnClientMoved?.Invoke(1, 1, "2", "1", "Fixture");
            OnServerMembershipChanged?.Invoke(1, 1, "Fixture", true);
        }
        public void Dispose() { Disposed = true; GuardedService?.Dispose(); }
    }
}
