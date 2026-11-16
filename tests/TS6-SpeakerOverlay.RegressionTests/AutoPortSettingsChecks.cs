using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Services;
using TS6_SpeakerOverlay.ViewModels;
using TS6_SpeakerOverlay.Views;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using Size = System.Windows.Size;

internal static class AutoPortSettingsChecks
{
    private static int _checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS: " + name);
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void Until(Func<bool> condition)
    {
        DateTime until = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < until) Pump(10);
        if (!condition()) throw new TimeoutException("Port UI fixture did not reach its expected state.");
    }

    private static void Await(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void Arrange(Window window)
    {
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(960, 720)); root.Arrange(new Rect(0, 0, 960, 720)); root.UpdateLayout();
    }

    internal static int Run()
    {
        var old = JsonSerializer.Deserialize<AppConfig>("{\"RemoteAppPort\":6200}")!;
        Check(old.AutoDetectRemoteAppPort && old.RemoteAppPort == 6200, "old config enables automatic discovery without losing remembered port");
        old.AutoDetectRemoteAppPort = false;
        var copied = JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(old))!;
        Check(!copied.AutoDetectRemoteAppPort && copied.RemoteAppPort == 6200, "manual connection mode survives config roundtrip");

        foreach (var language in LanguageHelper.SupportedLanguages)
        {
            var config = new AppConfig { Language = language.Code, RemoteAppPort = 6200 };
            var discovery = new AutoPortConnectionChecks.Discovery(new(true, true, [new("127.0.0.1", 7300, 100)]));
            var peers = new List<AutoPortConnectionChecks.Connection>();
            using var manager = new Ts6ConnectionManager(discovery, _ =>
            { var peer = new AutoPortConnectionChecks.Connection(); peers.Add(peer); return peer; }, true, 6200,
                TimeSpan.FromHours(1), action => System.Windows.Application.Current.Dispatcher.Invoke(action));
            using var vm = new MainViewModel(config, manager, false);
            var window = new SettingsWindow(vm);
            SettingsOrganizationChecks.Select(window, "Connection");
            var toggle = window.FindName("AutoDetectPortToggle") as CheckBox;
            var input = window.FindName("RemoteAppPortInput") as TextBox;
            var activePort = window.FindName("ActiveRemoteAppPortText") as TextBlock;
            var status = window.FindName("PortDetectionStatus") as TextBlock;
            Check(toggle != null && input != null && activePort != null && status != null, language.Code + " connection category exposes auto/manual controls and live status");
            Check(toggle!.IsChecked == true && !input!.IsEnabled, language.Code + " automatic mode disables manual editing");
            Await(manager.StartAsync());
            Arrange(window);
            Check(config.RemoteAppPort == 7300 && activePort!.Text == "7300" && vm.IsConnected,
                language.Code + " verified endpoint is saved and shown in the UI");
            Pump(50);
            Check(peers.Count == 1, language.Code + " saving a detected port does not trigger a reconnect loop");
            toggle.IsChecked = false;
            Until(() => peers.Count == 2);
            Arrange(window);
            Check(input!.IsEnabled, language.Code + " manual mode makes the existing port editable");
            input.Text = "7501";
            input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Until(() => manager.ActivePort == 7501);
            Arrange(window);
            Check(config.RemoteAppPort == 7501 && activePort!.Text == "7501", language.Code + " a custom manual port applies without app restart");
            input.Text = "0"; input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Check(Validation.GetHasError(input) && config.RemoteAppPort == 7501, language.Code + " port zero cannot replace a valid connection setting");
            input.Text = "65536"; input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Check(Validation.GetHasError(input) && config.RemoteAppPort == 7501, language.Code + " out-of-range port is rejected");
            discovery.Result = new(true, true, [new("127.0.0.1", 7400, 100)]);
            toggle.IsChecked = true;
            Until(() => manager.ActivePort == 7400);
            Arrange(window);
            Check(config.RemoteAppPort == 7400 && !input.IsEnabled && activePort!.Text == "7400",
                language.Code + " switching back to auto mode follows the current TS listener");
            Check(input.Text == "7400" && !Validation.GetHasError(input), language.Code + " automatic detection replaces stale invalid manual input");
            discovery.Result = new(true, true, []);
            peers.Last().Drop();
            Await(manager.DetectNowAsync());
            Pump(650);
            Arrange(window);
            Check(!vm.IsConnected && vm.ConnectionStatus == LanguageHelper.GetString("Lang_Port_NoListener"),
                language.Code + " specific no-listener diagnosis survives delayed disconnection cleanup");
            string beforeLanguage = status!.Text;
            LanguageHelper.SetLanguage(language.Code == "en-US" ? "de-DE" : "en-US");
            Arrange(window);
            Check(status.Text == LanguageHelper.GetString("Lang_Port_NoListener") && status.Text != beforeLanguage,
                language.Code + " port diagnosis relocalizes without losing its state");
            window.Close();
        }
        LanguageHelper.SetLanguage("en-US");
        return _checks;
    }

    internal static void RenderPreviews(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var language in LanguageHelper.SupportedLanguages)
        {
            var config = new AppConfig { Language = language.Code, RemoteAppPort = 6200 };
            var discovery = new AutoPortConnectionChecks.Discovery(new(true, true, [new("127.0.0.1", 7300, 100)]));
            using var manager = new Ts6ConnectionManager(discovery, _ => new AutoPortConnectionChecks.Connection(), true, 6200,
                TimeSpan.FromHours(1), action => System.Windows.Application.Current.Dispatcher.Invoke(action));
            using var vm = new MainViewModel(config, manager, false);
            var window = new SettingsWindow(vm);
            SettingsOrganizationChecks.Select(window, "Connection");
            Await(manager.StartAsync());
            Arrange(window);
            Save(window, Path.Combine(directory, "connection-auto-" + language.Code + ".png"));
            config.AutoDetectRemoteAppPort = false;
            Until(() => ((TextBox)window.FindName("RemoteAppPortInput")).IsEnabled);
            Arrange(window);
            Save(window, Path.Combine(directory, "connection-manual-" + language.Code + ".png"));
            window.Close();
        }
    }

    private static void Save(SettingsWindow window, string path)
    {
        var bitmap = new RenderTargetBitmap(960, 720, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render((FrameworkElement)window.Content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
