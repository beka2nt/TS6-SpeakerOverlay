using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Services;
using TS6_SpeakerOverlay.ViewModels;
using Application = System.Windows.Application;

internal static class Program
{
    private static readonly MethodInfo HandleMessage = typeof(Ts6Service)
        .GetMethod("HandleMessage", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static int _checks;

    [STAThread]
    private static int Main(string[] args)
    {
        _ = new Application();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        LanguageHelper.SetLanguage("en-US");
        try
        {
            if (args.Length == 2 && args[0] == "--preview")
            {
                FeatureChecks.RenderPreviews(args[1]);
                return 0;
            }
            if (args.Length == 1 && args[0] == "--check-github")
            {
                ReleaseInfo release = new UpdateService().GetLatestAsync(CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine($"GitHub latest: {release.Tag}; asset: {release.DownloadUri}");
                return 0;
            }
            MovementTests();
            TalkingTests();
            RefreshTests();
            _checks += FeatureChecks.Run();
            Console.WriteLine($"PASS: {_checks} regression checks. No TeamSpeak connection was opened.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    // Offline protocol fixtures: never start the socket or read/save a real API key.
    private static Ts6Service Service() => new(65534, loadApiKey: false);
    private static void Feed(Ts6Service service, string json) => HandleMessage.Invoke(service, [json]);
    private static string Snapshot(int friendId = 2, bool talking = false, string friendChannel = "10",
        string myChannel = "10", int connectionId = 1) => JsonSerializer.Serialize(new
    {
        type = "auth",
        payload = new
        {
            connections = new[]
            {
                new
                {
                    id = connectionId, clientId = 1,
                    properties = new { uniqueIdentifier = "regression-fixture", name = "Fixture" },
                    clientInfos = new[]
                    {
                        new { id = 1, channelId = myChannel, properties = new { nickname = "Self", flagTalking = false } },
                        new { id = friendId, channelId = friendChannel, properties = new { nickname = "Friend", flagTalking = talking } }
                    }
                }
            }
        }
    });
    private static string Move(int clientId, string from, string to, int connectionId = 1) =>
        JsonSerializer.Serialize(new { type = "clientMoved", payload = new
        { connectionId, clientId, oldChannelId = from, newChannelId = to, clientName = "Friend" } });
    private static string Talk(int clientId, bool talking, int connectionId = 1) =>
        JsonSerializer.Serialize(new { type = "talkStatusChanged", payload = new
        { connectionId, clientId, status = talking ? 1 : 0 } });

    private static void Check(bool success, string name)
    {
        if (!success) throw new InvalidOperationException($"FAIL: {name}");
        _checks++;
        Console.WriteLine($"PASS: {name}");
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void MovementTests()
    {
        using var service = Service();
        List<Ts6Server> latest = [];
        service.OnServersUpdated += servers => latest = servers;
        Feed(service, Snapshot());
        Feed(service, Move(2, "10", "20"));
        Check(latest[0].Users.Single(user => user.ClientId == 2).ChannelId == "20", "move updates immediately");
        Feed(service, Snapshot());
        Check(latest[0].Users.Single(user => user.ClientId == 2).ChannelId == "20", "stale auth cannot undo move");
        Feed(service, Snapshot(friendChannel: "20"));
        Feed(service, Snapshot());
        Check(latest[0].Users.Single(user => user.ClientId == 2).ChannelId == "20", "confirmed move survives reordered old auth");
        Feed(service, Move(2, "20", "10"));
        Feed(service, Snapshot(friendChannel: "20"));
        Check(latest[0].Users.Single(user => user.ClientId == 2).ChannelId == "10", "latest of rapid moves wins");
        Feed(service, Move(1, "10", "30"));
        Feed(service, Snapshot());
        Check(latest[0].MyChannelId == "30" && latest[0].Users.Single(user => user.ClientId == 1).ChannelId == "30",
            "own move protects both selected channel and user channel");
        Feed(service, Move(2, "10", "0"));
        Feed(service, Snapshot());
        Check(latest[0].Users.All(user => user.ClientId != 2), "stale auth cannot resurrect departed member");
        Feed(service, Snapshot(friendId: 3));
        Feed(service, Snapshot());
        Check(latest[0].Users.All(user => user.ClientId != 2), "departure remains guarded after confirming auth");
        Feed(service, Move(3, "0", "30"));
        Feed(service, Snapshot());
        Check(latest[0].Users.Any(user => user.ClientId == 3 && user.ChannelId == "30"), "rejoin with new client ID survives stale auth");
        Pump(5200);
        Feed(service, Snapshot());
        Check(latest[0].MyChannelId == "10" && latest[0].Users.Any(user => user.ClientId == 2)
            && latest[0].Users.All(user => user.ClientId != 3), "expired guards allow authoritative correction");

        using var otherService = Service();
        List<Ts6Server> servers = [];
        otherService.OnServersUpdated += value => servers = value;
        var first = System.Text.Json.Nodes.JsonNode.Parse(Snapshot())!;
        var second = System.Text.Json.Nodes.JsonNode.Parse(Snapshot(connectionId: 2))!;
        first["payload"]!["connections"]!.AsArray().Add(second["payload"]!["connections"]![0]!.DeepClone());
        Feed(otherService, first.ToJsonString());
        Feed(otherService, Move(2, "10", "20", connectionId: 2));
        Check(servers.Single(server => server.ConnectionId == 1).Users.Single(user => user.ClientId == 2).ChannelId == "10"
            && servers.Single(server => server.ConnectionId == 2).Users.Single(user => user.ClientId == 2).ChannelId == "20",
            "same client ID on different servers stays isolated");
    }

    private static void TalkingTests()
    {
        using var service = Service();
        using var vm = new MainViewModel(new AppConfig { Language = "en-US", EnableNotifications = false }, service, false);
        Feed(service, Snapshot());
        Feed(service, Snapshot(talking: true));
        Check(vm.Users.Single(user => user.ClientId == 2).IsTalking, "refresh repairs missed speaking-start event");
        Feed(service, Snapshot());
        Check(!vm.Users.Single(user => user.ClientId == 2).IsTalking, "refresh repairs missed speaking-stop event");
        Feed(service, Talk(2, true));
        Feed(service, Snapshot());
        Check(vm.Users.Single(user => user.ClientId == 2).IsTalking, "recent speaking event wins over stale snapshot");
        Feed(service, Talk(2, false));
        Feed(service, Snapshot(talking: true));
        Check(!vm.Users.Single(user => user.ClientId == 2).IsTalking, "recent stop event wins over stale snapshot");
        Pump(1100);
        Feed(service, Snapshot(talking: true));
        Check(vm.Users.Single(user => user.ClientId == 2).IsTalking, "expired speaking guard permits snapshot repair");
        Feed(service, Move(2, "10", "0"));
        Feed(service, Move(3, "0", "10"));
        Feed(service, Talk(3, true));
        Check(vm.Users.All(user => user.ClientId != 2) && vm.Users.Single(user => user.ClientId == 3).IsTalking,
            "rejoined member lights up with new client ID");
    }

    private static void RefreshTests()
    {
        using var service = Service();
        using var vm = new MainViewModel(new AppConfig { Language = "en-US", EnableNotifications = false }, service, false);
        Feed(service, Snapshot());
        vm.RefreshData();
        Check(vm.ConnectionStatus == LanguageHelper.GetString("Lang_Status_RemoteUnavailable"), "disconnected refresh is rejected");
        vm.IsConnected = true;
        vm.RefreshData();
        Feed(service, Move(2, "10", "20"));
        Check(vm.ConnectionStatus == LanguageHelper.GetString("Lang_Status_Refreshing"), "movement is not mistaken for refresh response");
        Feed(service, "{\"type\":\"auth\",\"payload\":{}}");
        Check(vm.ConnectionStatus == LanguageHelper.GetString("Lang_Status_Refreshing"), "incomplete auth is not a successful refresh");
        Pump(6500);
        Check(vm.ConnectionStatus == LanguageHelper.GetString("Lang_Status_RefreshTimeout"), "no-response refresh retries then times out");
        vm.RefreshData();
        Check(vm.ConnectionStatus == LanguageHelper.GetString("Lang_Status_Refreshing"), "refresh can be retried after timeout");
        Feed(service, Snapshot());
        Check(vm.ConnectionStatus == "", "valid snapshot completes refresh");
        vm.RefreshData();
        Pump(100);
        vm.RefreshData();
        Feed(service, Snapshot());
        Pump(6500);
        Check(vm.ConnectionStatus == "", "cancelled refresh timers cannot overwrite later success");
        vm.RefreshData();
        Feed(service, "{\"type\":\"auth\",\"payload\":{\"connections\":[]}}");
        Check(vm.ConnectionStatus == LanguageHelper.GetString("Lang_Status_Waiting"), "empty valid snapshot finishes refresh with waiting status");
        vm.RefreshData();
        vm.Dispose();
        Pump(6500);
        Check(!service.SendAuth(), "shutdown cancels pending refresh and prevents sends");
    }
}
