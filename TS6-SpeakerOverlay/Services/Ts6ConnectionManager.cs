using TS6_SpeakerOverlay.Models;

namespace TS6_SpeakerOverlay.Services;

internal enum Ts6PortState { WaitingForTeamSpeak, Searching, Connecting, Connected, NoRemoteAppListener, AmbiguousCandidates, AuthorizationRequired, InspectionFailed }

internal sealed class Ts6ConnectionManager : ITs6Connection
{
    private sealed class Session(ITs6Connection connection, Ts6Endpoint endpoint, long revision, bool automatic)
    {
        internal readonly ITs6Connection Connection = connection;
        internal readonly Ts6Endpoint Endpoint = endpoint;
        internal readonly long Revision = revision;
        internal readonly bool Automatic = automatic;
        internal bool Active = true, TransportConnected, Verified;
    }

    private readonly object _sync = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _token;
    private readonly ITs6PortDiscovery _discovery;
    private readonly Func<Ts6Endpoint, ITs6Connection> _createConnection;
    private readonly Action<Action> _dispatch;
    private readonly TimeSpan _interval;
    private Session? _current;
    private Task? _loop;
    private long _revision;
    private bool _automatic, _started, _authorizationBlocked, _connected;
    private int _configuredPort;
    private volatile bool _disposed;
    private Ts6PortState _state = Ts6PortState.Searching;

    public event Action<List<Ts6Server>>? OnServersUpdated;
    public event Action? OnSnapshotReceived;
    public event Action<int, int, bool>? OnTalkStatusChanged;
    public event Action<int, int, bool?, bool?, bool?>? OnUserPropertiesChanged;
    public event Action<int, int, string, string, string?>? OnClientMoved;
    public event Action<int, int, string, bool>? OnServerMembershipChanged;
    public event Action<bool>? OnConnectionStateChanged;
    public event Action<int>? OnAuthenticationRejected;
    internal event Action<Ts6Endpoint>? OnEndpointVerified;
    internal event Action<Ts6PortState>? OnPortStateChanged;

    internal int? ActivePort
    {
        get { lock (_sync) return _current is { Active: true, TransportConnected: true } session ? session.Endpoint.Port : null; }
    }
    internal Ts6PortState State { get { lock (_sync) return _state; } }

    // dispatch must deliver synchronously. The WPF caller uses Dispatcher.Invoke,
    // so the current-session check runs when the UI actually receives the event.
    internal Ts6ConnectionManager(ITs6PortDiscovery discovery, Func<Ts6Endpoint, ITs6Connection> createConnection,
        bool automatic, int configuredPort, TimeSpan? detectionInterval = null, Action<Action>? dispatch = null)
    {
        _discovery = discovery;
        _createConnection = createConnection;
        _automatic = automatic;
        _configuredPort = configuredPort is >= 1 and <= 65535 ? configuredPort : 5899;
        _interval = detectionInterval ?? TimeSpan.FromSeconds(5);
        if (_interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(detectionInterval));
        _dispatch = dispatch ?? (action => action());
        _token = _lifetime.Token;
    }

    public Task StartAsync()
    {
        lock (_sync)
        {
            if (_disposed || _started) return Task.CompletedTask;
            _started = true;
            _loop = PollAsync();
        }
        return AttemptAsync(false);
    }

    internal Task ConfigureAsync(bool automatic, int port)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        Session? previous;
        long revision;
        bool started;
        lock (_sync)
        {
            if (_disposed) return Task.CompletedTask;
            if (_automatic == automatic && _configuredPort == port) return Task.CompletedTask;
            _automatic = automatic;
            _configuredPort = port;
            _authorizationBlocked = false;
            revision = ++_revision;
            previous = _current;
            if (previous != null) previous.Active = false;
            _current = null;
            started = _started;
        }
        previous?.Connection.Dispose();
        PublishRevision(revision, () => SetDisconnected(revision));
        return started ? AttemptAsync(true) : Task.CompletedTask;
    }

    internal Task DetectNowAsync()
    {
        bool started;
        lock (_sync)
        {
            if (_disposed) return Task.CompletedTask;
            _authorizationBlocked = false;
            started = _started;
        }
        return started ? AttemptAsync(true) : StartAsync();
    }

    private async Task PollAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(_interval, _token).ConfigureAwait(false);
                await AttemptAsync(false).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
    }

    private async Task AttemptAsync(bool force)
    {
        if (_disposed) return;
        bool entered = false;
        long revision = -1;
        Session? created = null;
        try
        {
            await _gate.WaitAsync(_token).ConfigureAwait(false);
            entered = true;
            bool automatic, hasTransport;
            int port;
            lock (_sync)
            {
                if (_disposed || _authorizationBlocked) return;
                if (!force && _current is { Active: true, TransportConnected: true }) return;
                automatic = _automatic;
                port = _configuredPort;
                revision = _revision;
                hasTransport = _current is { Active: true, TransportConnected: true };
            }

            Ts6Endpoint? endpoint;
            if (automatic)
            {
                if (!hasTransport) PublishRevision(revision, () => SetState(Ts6PortState.Searching, revision));
                var result = await _discovery.DiscoverAsync(_token).ConfigureAwait(false);
                if (!IsRevision(revision)) return;
                Ts6PortState? failure = !result.InspectionSucceeded ? Ts6PortState.InspectionFailed
                    : !result.IsTeamSpeakRunning ? Ts6PortState.WaitingForTeamSpeak
                    : result.Candidates.Count == 0 ? Ts6PortState.NoRemoteAppListener : null;
                if (failure.HasValue) { DiscoveryFailed(failure.Value, revision); return; }
                var choices = result.Candidates.GroupBy(item => (item.ProcessId, item.Port))
                    .Select(group => group.OrderBy(item => item.Host == "127.0.0.1" ? 0 : 1).First()).ToArray();
                var remembered = choices.Where(item => item.Port == port).ToArray();
                endpoint = remembered.Length == 1 ? remembered[0] : choices.Length == 1 ? choices[0] : null;
                if (endpoint == null) { DiscoveryFailed(Ts6PortState.AmbiguousCandidates, revision); return; }
                if (endpoint.ProcessId <= 0 || !_discovery.IsOwnedByTeamSpeak(endpoint))
                { DiscoveryFailed(Ts6PortState.NoRemoteAppListener, revision); return; }
            }
            else endpoint = new("127.0.0.1", port, 0);
            _ = endpoint.ToUri();

            Session? previous;
            lock (_sync)
            {
                if (_disposed || revision != _revision) return;
                if (_current is { Active: true, TransportConnected: true } current && current.Endpoint == endpoint) return;
                previous = _current;
                if (previous != null) previous.Active = false;
                _current = null;
            }
            previous?.Connection.Dispose();
            PublishRevision(revision, () => { SetDisconnected(revision); SetState(Ts6PortState.Connecting, revision); });
            var connection = _createConnection(endpoint);
            created = new(connection, endpoint, revision, automatic);
            lock (_sync)
            {
                if (_disposed || revision != _revision) { connection.Dispose(); return; }
                _current = created;
            }
            Attach(created);
            await connection.StartAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (_disposed) { }
        catch (Exception error)
        {
            AppLogger.Error("Unable to establish a TS6 Remote Apps session.", error);
            if (created != null) Publish(created, () => Retire(created, Ts6PortState.NoRemoteAppListener, false));
            else if (revision >= 0) DiscoveryFailed(Ts6PortState.InspectionFailed, revision);
        }
        finally { if (entered) _gate.Release(); }
    }

    private void Attach(Session session)
    {
        var connection = session.Connection;
        connection.OnConnectionStateChanged += connected => Publish(session, () =>
        {
            if (!connected) { Retire(session, Ts6PortState.NoRemoteAppListener, false); return; }
            if (session.Automatic && !_discovery.IsOwnedByTeamSpeak(session.Endpoint))
            { Retire(session, Ts6PortState.NoRemoteAppListener, false); return; }
            lock (_sync) { if (!IsCurrentLocked(session)) return; session.TransportConnected = true; }
            SetState(Ts6PortState.AuthorizationRequired, session.Revision);
        });
        connection.OnAuthenticationRejected += code => Publish(session, () =>
        {
            Retire(session, Ts6PortState.AuthorizationRequired, true);
            if (IsRevision(session.Revision)) OnAuthenticationRejected?.Invoke(code);
        });
        connection.OnSnapshotReceived += () => Publish(session, () =>
        {
            bool first;
            lock (_sync)
            {
                if (!IsCurrentLocked(session) || !session.TransportConnected) return;
                first = !session.Verified;
                session.Verified = true;
                if (session.Automatic) _configuredPort = session.Endpoint.Port;
            }
            if (first) OnEndpointVerified?.Invoke(session.Endpoint);
            if (!IsCurrent(session)) return;
            SetState(Ts6PortState.Connected, session.Revision);
            if (!IsCurrent(session)) return;
            bool notify;
            lock (_sync) { notify = !_connected; _connected = true; }
            if (notify) OnConnectionStateChanged?.Invoke(true);
            if (IsCurrent(session)) OnSnapshotReceived?.Invoke();
        });
        connection.OnServersUpdated += servers => Publish(session, () => OnServersUpdated?.Invoke(servers), true);
        connection.OnTalkStatusChanged += (server, client, talking) => Publish(session, () => OnTalkStatusChanged?.Invoke(server, client, talking), true);
        connection.OnUserPropertiesChanged += (server, client, input, output, away) => Publish(session, () => OnUserPropertiesChanged?.Invoke(server, client, input, output, away), true);
        connection.OnClientMoved += (server, client, next, old, name) => Publish(session, () => OnClientMoved?.Invoke(server, client, next, old, name), true);
        connection.OnServerMembershipChanged += (server, client, name, joined) => Publish(session, () => OnServerMembershipChanged?.Invoke(server, client, name, joined), true);
    }

    private void Retire(Session session, Ts6PortState state, bool authorizationBlocked)
    {
        lock (_sync)
        {
            if (!IsCurrentLocked(session)) return;
            session.Active = false;
            _current = null;
            _authorizationBlocked = authorizationBlocked;
        }
        session.Connection.Dispose();
        SetDisconnected(session.Revision);
        SetState(state, session.Revision);
    }

    private void DiscoveryFailed(Ts6PortState state, long revision) => PublishRevision(revision, () =>
    {
        lock (_sync) { if (_current is { Active: true, TransportConnected: true }) return; }
        SetState(state, revision);
    });

    private void SetDisconnected(long revision)
    {
        bool notify;
        lock (_sync)
        {
            if (_disposed || revision != _revision) return;
            notify = _connected;
            _connected = false;
        }
        if (notify) OnConnectionStateChanged?.Invoke(false);
    }

    private void SetState(Ts6PortState state, long revision)
    {
        lock (_sync)
        {
            if (_disposed || revision != _revision) return;
            _state = state;
        }
        OnPortStateChanged?.Invoke(state);
    }

    private bool IsRevision(long revision) { lock (_sync) return !_disposed && revision == _revision; }
    private bool IsCurrentLocked(Session session) => !_disposed && session.Active && session.Revision == _revision && ReferenceEquals(_current, session);
    private bool IsCurrent(Session session) { lock (_sync) return IsCurrentLocked(session); }

    private void Publish(Session session, Action action, bool requireTransport = false)
    {
        if (_disposed) return;
        _dispatch(() =>
        {
            lock (_sync) { if (!IsCurrentLocked(session) || (requireTransport && !session.TransportConnected)) return; }
            action();
        });
    }

    private void PublishRevision(long revision, Action action)
    {
        if (_disposed) return;
        _dispatch(() => { if (IsRevision(revision)) action(); });
    }

    public bool SendAuth()
    {
        Session? session;
        lock (_sync) session = !_disposed && _current is { Active: true, Verified: true } current ? current : null;
        return session?.Connection.SendAuth() == true;
    }

    public void Dispose()
    {
        Session? previous;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            ++_revision;
            previous = _current;
            if (previous != null) previous.Active = false;
            _current = null;
        }
        _lifetime.Cancel();
        previous?.Connection.Dispose();
        _ = ReleaseResourcesAsync();
    }

    private async Task ReleaseResourcesAsync()
    {
        if (_loop != null) await _loop.ConfigureAwait(false);
        await _gate.WaitAsync().ConfigureAwait(false);
        _gate.Release();
        _gate.Dispose();
        _lifetime.Dispose();
    }
}
