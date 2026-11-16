using System.Net;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using TS6_SpeakerOverlay.Helpers;

namespace TS6_SpeakerOverlay.Services;

internal sealed record Ts6Endpoint(string Host, int Port, int ProcessId)
{
    internal Uri ToUri()
    {
        if (Port is < 1 or > 65535 || !IPAddress.TryParse(Host, out var address) || !IPAddress.IsLoopback(address))
            throw new ArgumentException("Remote Apps endpoints must use a valid loopback address and port.");
        return new UriBuilder("ws", Host, Port).Uri;
    }
}

internal sealed record Ts6DiscoveryResult(bool IsTeamSpeakRunning, bool InspectionSucceeded, IReadOnlyList<Ts6Endpoint> Candidates);
internal interface ITs6PortDiscovery
{
    Task<Ts6DiscoveryResult> DiscoverAsync(CancellationToken token);
    bool IsOwnedByTeamSpeak(Ts6Endpoint endpoint);
}

internal sealed class WindowsTs6PortDiscovery : ITs6PortDiscovery
{
    private readonly Func<IReadOnlyList<int>> _processIds;
    private readonly Func<IReadOnlyList<TcpListenerOwner>> _listeners;

    internal WindowsTs6PortDiscovery(Func<IReadOnlyList<int>>? processIds = null, Func<IReadOnlyList<TcpListenerOwner>>? listeners = null)
    {
        _processIds = processIds ?? ReadTeamSpeakIds;
        _listeners = listeners ?? WindowsTcpListenerTable.Read;
    }

    public Task<Ts6DiscoveryResult> DiscoverAsync(CancellationToken token) => Task.Run(() => Read(token), token);

    public bool IsOwnedByTeamSpeak(Ts6Endpoint endpoint)
    {
        var current = Read(CancellationToken.None);
        return current.InspectionSucceeded && current.Candidates.Contains(endpoint);
    }

    private Ts6DiscoveryResult Read(CancellationToken token)
    {
        bool running = false;
        try
        {
            token.ThrowIfCancellationRequested();
            var ids = _processIds().Where(pid => pid > 0).ToHashSet();
            running = ids.Count > 0;
            if (!running) return new(false, true, []);
            var candidates = new HashSet<Ts6Endpoint>();
            foreach (var row in _listeners())
            {
                token.ThrowIfCancellationRequested();
                if (!ids.Contains(row.ProcessId) || row.Port is < 1 or > 65535) continue;
                string? host = row.Address.Equals(IPAddress.Any) ? "127.0.0.1"
                    : row.Address.Equals(IPAddress.IPv6Any) ? "::1"
                    : IPAddress.IsLoopback(row.Address) ? row.Address.ToString() : null;
                if (host != null) candidates.Add(new(host, row.Port, row.ProcessId));
            }
            return new(true, true, candidates.OrderBy(item => item.Port).ThenBy(item => item.Host).ToArray());
        }
        catch (Exception error) when (error is Win32Exception or UnauthorizedAccessException or InvalidOperationException or IOException)
        {
            AppLogger.Warning($"Unable to inspect TS6 listeners: {error.GetType().Name}.");
            return new(running, false, []);
        }
    }

    private static IReadOnlyList<int> ReadTeamSpeakIds()
    {
        var processes = Process.GetProcessesByName("TeamSpeak");
        try { return processes.Select(process => process.Id).ToArray(); }
        finally { foreach (var process in processes) process.Dispose(); }
    }
}
