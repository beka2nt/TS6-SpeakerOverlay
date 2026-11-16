using System.Net;
using System.Net.Sockets;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Services;

internal static class PortDiscoveryChecks
{
    private static int _checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS: " + name);
    }

    internal static int Run()
    {
        IReadOnlyList<TcpListenerOwner> listeners = [
            new(IPAddress.Loopback, 6200, 100), new(IPAddress.Loopback, 6100, 999),
            new(IPAddress.Any, 7000, 200), new(IPAddress.IPv6Loopback, 6300, 100),
            new(IPAddress.IPv6Any, 7100, 200), new(IPAddress.Loopback, 6200, 100),
            new(IPAddress.Parse("192.0.2.1"), 7300, 100), new(IPAddress.Loopback, 0, 100),
            new(IPAddress.Loopback, 65536, 100)];
        var discovery = new WindowsTs6PortDiscovery(() => [100, 200], () => listeners);
        var result = discovery.DiscoverAsync(CancellationToken.None).GetAwaiter().GetResult();
        Check(result.Candidates.Select(endpoint => $"{endpoint.Host}|{endpoint.Port}|{endpoint.ProcessId}").ToHashSet()
            .SetEquals(["127.0.0.1|6200|100", "127.0.0.1|7000|200", "::1|6300|100", "::1|7100|200"]),
            "discovery returns only TS-owned local endpoints and normalizes wildcard listeners");
        Check(result.IsTeamSpeakRunning && result.InspectionSucceeded, "available TS process and successful inspection stay distinct");
        Check(discovery.IsOwnedByTeamSpeak(new("127.0.0.1", 6200, 100)), "owner verification accepts the live TS listener");
        Check(!discovery.IsOwnedByTeamSpeak(new("127.0.0.1", 6100, 999)), "remembered port owned by another process is never trusted");
        listeners = [new(IPAddress.Loopback, 6200, 999)];
        Check(!discovery.IsOwnedByTeamSpeak(new("127.0.0.1", 6200, 100)), "ownership is re-read after a port changes hands");
        var stopped = new WindowsTs6PortDiscovery(() => [], () => throw new InvalidOperationException("Must not read listeners without TS"))
            .DiscoverAsync(CancellationToken.None).GetAwaiter().GetResult();
        Check(!stopped.IsTeamSpeakRunning && stopped.InspectionSucceeded && stopped.Candidates.Count == 0,
            "TS not running is not confused with an inspection failure");
        var unavailable = new WindowsTs6PortDiscovery(() => [100], () => throw new UnauthorizedAccessException())
            .DiscoverAsync(CancellationToken.None).GetAwaiter().GetResult();
        Check(unavailable.IsTeamSpeakRunning && !unavailable.InspectionSucceeded && unavailable.Candidates.Count == 0,
            "inspection denial cannot appear as successful empty discovery");
        var noListener = new WindowsTs6PortDiscovery(() => [100], () => []).DiscoverAsync(CancellationToken.None).GetAwaiter().GetResult();
        Check(noListener.IsTeamSpeakRunning && noListener.InspectionSucceeded && noListener.Candidates.Count == 0,
            "running TS without an interface remains an explicit no-listener result");
        Check(new Ts6Endpoint("::1", 6300, 100).ToUri().AbsoluteUri == "ws://[::1]:6300/", "IPv6 endpoint produces a valid loopback WebSocket URI");
        bool remoteRejected = false;
        try { _ = new Ts6Endpoint("198.51.100.1", 6300, 100).ToUri(); }
        catch (ArgumentException) { remoteRejected = true; }
        Check(remoteRejected, "automatic endpoint cannot form a remote-network URI");
        NativeListener(IPAddress.Loopback);
        if (Socket.OSSupportsIPv6) NativeListener(IPAddress.IPv6Loopback);
        return _checks;
    }

    private static void NativeListener(IPAddress address)
    {
        var listener = new TcpListener(address, 0);
        try
        {
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var reader = new WindowsTs6PortDiscovery(() => [Environment.ProcessId]);
            var found = reader.DiscoverAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(found.InspectionSucceeded && found.Candidates.Any(endpoint => endpoint.Host == address.ToString()
                && endpoint.Port == port && endpoint.ProcessId == Environment.ProcessId), address + " native listener retains exact port and PID");
            Check(reader.IsOwnedByTeamSpeak(new(address.ToString(), port, Environment.ProcessId)), address + " native owner recheck matches the fixture listener");
        }
        finally { listener.Stop(); }
    }
}
