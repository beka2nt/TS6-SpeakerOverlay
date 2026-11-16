using System.Reflection;
using System.Text.Json;
using TS6_SpeakerOverlay.Services;

internal static class SessionKeyChecks
{
    private static readonly MethodInfo HandleMessage = typeof(Ts6Service).GetMethod("HandleMessage", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static void Feed(Ts6Service service, string key, bool snapshot = true, int status = 0)
        => HandleMessage.Invoke(service, [JsonSerializer.Serialize(new
        {
            type = "auth", status = new { code = status },
            payload = snapshot ? (object)new { apiKey = key, connections = Array.Empty<object>() } : new { apiKey = key }
        })]);

    internal static int Run() => Task.Run(RunAsync).GetAwaiter().GetResult();
    private static async Task<int> RunAsync()
    {
        int checks = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("FAIL: " + name);
            checks++;
            Console.WriteLine("PASS: " + name);
        }
        var endpoint = new Ts6Endpoint("127.0.0.1", 6200, 100);
        var writes = new List<string>();
        using var service = new Ts6Service(endpoint, false, false, writes.Add);
        var peer = new AutoPortConnectionChecks.Connection
        {
            GuardedService = service, AutoSnapshot = false,
            BeforeConnect = () => Feed(service, "fixture-before-owner-check")
        };
        int created = 0;
        using (var manager = new Ts6ConnectionManager(new AutoPortConnectionChecks.Discovery(new(true, true, [endpoint])),
            _ => created++ == 0 ? peer : new AutoPortConnectionChecks.Connection(), true, 6200))
        {
            await manager.StartAsync();
            Check(writes.Count == 0, "a managed auth message arriving before ownership approval cannot write a key");
            Feed(service, "fixture-incomplete", snapshot: false);
            Check(writes.Count == 0, "an auth response without a valid snapshot cannot write a key");
            Feed(service, "fixture-rejected", status: 403);
            Check(writes.Count == 0, "rejected authorization cannot write a key");
            Feed(service, "fixture-current");
            Check(writes.SequenceEqual(["fixture-current"]), "current ownership-approved valid authorization saves through the recording store");
            Feed(service, "fixture-current");
            Check(writes.Count == 1, "repeated snapshots do not rewrite an unchanged key");
            await manager.ConfigureAsync(false, 7400);
            Feed(service, "fixture-retired");
            Check(writes.SequenceEqual(["fixture-current"]), "retired-session authorization cannot overwrite the current shared key");
        }
        var directWrites = new List<string>();
        using var direct = new Ts6Service(endpoint, false, true, directWrites.Add);
        Feed(direct, "fixture-direct-incomplete", snapshot: false);
        Check(directWrites.Count == 0, "direct-service malformed auth also preserves the saved key");
        Feed(direct, "fixture-direct");
        Check(directWrites.SequenceEqual(["fixture-direct"]), "direct-service valid auth retains standalone compatibility");
        direct.Dispose();
        Feed(direct, "fixture-after-dispose");
        Check(directWrites.SequenceEqual(["fixture-direct"]), "an in-flight callback after service disposal cannot persist a key");
        return checks;
    }
}
