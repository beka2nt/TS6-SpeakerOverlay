using TS6_SpeakerOverlay.Models;

namespace TS6_SpeakerOverlay.Services;

public interface ITs6Connection : IDisposable
{
    event Action<List<Ts6Server>>? OnServersUpdated;
    event Action? OnSnapshotReceived;
    event Action<int, int, bool>? OnTalkStatusChanged;
    event Action<int, int, bool?, bool?, bool?>? OnUserPropertiesChanged;
    event Action<int, int, string, string, string?>? OnClientMoved;
    event Action<int, int, string, bool>? OnServerMembershipChanged;
    // A transport raises true before automatic auth; the manager facade raises
    // true only once a valid snapshot has verified the connection.
    event Action<bool>? OnConnectionStateChanged;
    event Action<int>? OnAuthenticationRejected;
    Task StartAsync();
    bool SendAuth();
}
