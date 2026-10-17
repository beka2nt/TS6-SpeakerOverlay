using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Services;
using TS6_SpeakerOverlay.ViewModels;
using TS6_SpeakerOverlay.Views;
using Application = System.Windows.Application;

internal static class FeatureChecks
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
        UpdateTests();
        LanguageTests();
        LayoutTests();
        return _checks;
    }

    private static string Release(string tag = "v9.0.0", string host = "github.com", bool prerelease = false,
        string state = "uploaded") => JsonSerializer.Serialize(new
    {
        tag_name = tag, draft = false, prerelease,
        html_url = $"https://{host}/beka2nt/TS6-SpeakerOverlay/releases/tag/{tag}",
        assets = new[] { new { name = "TS6-SpeakerOverlay.exe", state, size = 100,
            browser_download_url = $"https://{host}/beka2nt/TS6-SpeakerOverlay/releases/download/{tag}/TS6-SpeakerOverlay.exe" } }
    });

    private static void Await(Task task)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < timeout)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
        if (!task.IsCompleted) throw new TimeoutException("Fixture did not finish.");
        task.GetAwaiter().GetResult();
    }

    private static void UpdateTests()
    {
        Check(UpdateService.IsNewer(new Version("1.10.0"), new Version("1.9.9")), "numeric version comparison");
        Check(!UpdateService.IsNewer(new Version("1.6.0"), new Version("1.6.0.0")), "three/four-part versions compare equally");
        Check(!UpdateService.IsNewer(new Version("1.5.1"), new Version("1.6.0")), "older release cannot trigger downgrade");
        Check(UpdateService.IsNewer(new Version("1.6.1"), new Version("1.6.0")), "patch release is detected");
        ReleaseInfo info = UpdateService.ParseRelease(Release());
        Check(info.Tag == "v9.0.0" && info.DownloadUri?.Host == "github.com", "stable release and exe parsed");
        Check(UpdateService.ParseRelease(Release(state: "new")).DownloadUri == null, "unfinished asset falls back to release page");
        foreach (string invalid in new[] { Release(host: "evil.example"), Release(prerelease: true), Release(tag: "not-a-version") })
        {
            bool rejected = false;
            try { UpdateService.ParseRelease(invalid); } catch (FormatException) { rejected = true; }
            Check(rejected, "invalid/untrusted release is rejected");
        }
        Check(!UpdateService.IsTrustedReleaseUrl("https://github.com@evil.example/beka2nt/TS6-SpeakerOverlay/releases/tag/v9", "tag/"),
            "URL user-info cannot spoof GitHub");
        Check(!UpdateService.IsTrustedReleaseUrl("http://github.com/beka2nt/TS6-SpeakerOverlay/releases/tag/v9", "tag/"), "HTTPS is required");

        using var service = new Ts6Service(65534, false);
        using var handler = new FixtureHandler();
        using var client = new HttpClient(handler);
        using var vm = new MainViewModel(new AppConfig { Language = "en-US" }, service, false, new UpdateService(client));
        Await(vm.CheckForUpdatesCommand.ExecuteAsync(null));
        Check(vm.IsUpdateAvailable && vm.UpdateCheckState == UpdateCheckState.Available, "new release updates shared UI state");
        Check(handler.RequestValid, "request uses official latest endpoint and User-Agent");
        handler.Json = Release("v" + UpdateService.CurrentVersion);
        Await(vm.CheckForUpdatesCommand.ExecuteAsync(null));
        Check(!vm.IsUpdateAvailable && vm.UpdateCheckState == UpdateCheckState.Current, "current release is not offered as update");
        handler.Code = HttpStatusCode.TooManyRequests;
        Await(vm.CheckForUpdatesCommand.ExecuteAsync(null));
        Check(vm.UpdateCheckState == UpdateCheckState.RateLimited, "rate limit has actionable UI status");
        handler.Code = HttpStatusCode.InternalServerError;
        Await(vm.CheckForUpdatesCommand.ExecuteAsync(null));
        Check(vm.UpdateCheckState == UpdateCheckState.Failed, "network/server failure ends checking state");
        handler.Code = HttpStatusCode.OK;
        handler.Json = "{}";
        Await(vm.CheckForUpdatesCommand.ExecuteAsync(null));
        Check(vm.UpdateCheckState == UpdateCheckState.Failed, "malformed response ends checking state");
        handler.Json = Release();
        Await(vm.CheckForUpdatesCommand.ExecuteAsync(null));
        Check(vm.UpdateCheckState == UpdateCheckState.Available, "retry after failure succeeds");
        string english = vm.UpdateStatusText;
        LanguageHelper.SetLanguage("ko-KR");
        Check(vm.UpdateStatusText != english && vm.UpdateStatusText.Contains("v9.0.0"), "update status relocalizes immediately");
        handler.Block = true;
        int before = handler.Calls;
        Task pending = vm.CheckForUpdatesCommand.ExecuteAsync(null);
        Check(!vm.CheckForUpdatesCommand.CanExecute(null), "update button is disabled during a request");
        Await(vm.CheckForUpdatesCommand.ExecuteAsync(null));
        Check(handler.Calls == before + 1, "repeated check does not issue parallel requests");
        vm.Dispose();
        Await(pending);
        Check(handler.Cancelled, "shutdown cancels in-flight update request");
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        internal string Json = Release();
        internal HttpStatusCode Code = HttpStatusCode.OK;
        internal bool Block, Cancelled, RequestValid;
        internal int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            RequestValid = request.RequestUri?.AbsoluteUri == "https://api.github.com/repos/beka2nt/TS6-SpeakerOverlay/releases/latest"
                && request.Headers.UserAgent.ToString().Contains("TS6-SpeakerOverlay/")
                && !request.Headers.Contains("Authorization");
            if (Block)
            {
                try { await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { Cancelled = true; throw; }
            }
            return new HttpResponseMessage(Code) { Content = new StringContent(Json) };
        }
    }

    private static void LanguageTests()
    {
        Check(LanguageHelper.SupportedLanguages.Count == 7, "seven native language choices available");
        LanguageHelper.SetLanguage("en-US");
        var english = Application.Current.Resources.MergedDictionaries.Last();
        string[] keys = english.Keys.Cast<string>().Order().ToArray();
        var styles = new ResourceDictionary { ["FixtureStyle"] = "preserve" };
        Application.Current.Resources.MergedDictionaries.Add(styles);
        foreach (var language in LanguageHelper.SupportedLanguages)
        {
            LanguageHelper.SetLanguage(language.Code);
            var dictionary = Application.Current.Resources.MergedDictionaries.Last();
            Check(keys.SequenceEqual(dictionary.Keys.Cast<string>().Order()), language.Code + " has all resource keys");
            Check(keys.All(key => !string.IsNullOrWhiteSpace(dictionary[key] as string)), language.Code + " has no empty translations");
            Check(keys.All(key => Regex.Matches((string)english[key], @"\{\d+\}").Select(match => match.Value)
                .SequenceEqual(Regex.Matches((string)dictionary[key], @"\{\d+\}").Select(match => match.Value))),
                language.Code + " preserves format placeholders");
            using var service = new Ts6Service(65534, false);
            using var vm = new MainViewModel(new AppConfig { Language = language.Code }, service, false);
            var window = new SettingsWindow(vm);
            var root = (FrameworkElement)window.Content;
            root.Measure(new System.Windows.Size(960, 720));
            root.Arrange(new Rect(0, 0, 960, 720));
            root.UpdateLayout();
            var selector = (System.Windows.Controls.ComboBox)window.FindName("LanguageSelector");
            Check(selector.Items.Count == 7 && (string?)selector.SelectedValue == language.Code,
                language.Code + " settings binds native language selector");
            Check(selector.TransformToAncestor(root).Transform(new System.Windows.Point()).Y < 146,
                language.Code + " language is visible at top without scrolling");
            window.Close();
        }
        Check(Application.Current.Resources.MergedDictionaries.Contains(styles), "language switching preserves unrelated styles");
        Application.Current.Resources.MergedDictionaries.Remove(styles);
        LanguageHelper.SetLanguage("../invalid");
        Check(LanguageHelper.CurrentLanguage == "en-US", "unsupported language falls back safely");
    }

    private static void LayoutTests()
    {
        using (var menu = TrayIconHelper.CreateMenuShell())
        {
            menu.CreateControl();
            var item = TrayIconHelper.CreateMenuItem("A long settings and language label", TrayIconHelper.MenuGlyph.Settings);
            menu.Items.Add(item);
            TrayIconHelper.LayoutMenu(menu);
            var layout = TrayMenuLayout.Calculate(item.Width, item.Height, menu.DeviceDpi);
            int textWidth = System.Windows.Forms.TextRenderer.MeasureText(item.Text, item.Font,
                System.Drawing.Size.Empty, System.Windows.Forms.TextFormatFlags.SingleLine | System.Windows.Forms.TextFormatFlags.NoPadding).Width;
            Check(layout.Text.Width >= textWidth, "actual menu width fits long label without clipping");
            using var image = RenderMenu(menu);
            int leftPixels = 0, rightPixels = 0;
            for (int x = item.Bounds.Left + layout.Icon.Left; x < item.Bounds.Left + layout.Icon.Right; x++)
                for (int y = item.Bounds.Top + layout.Icon.Top; y < item.Bounds.Top + layout.Icon.Bottom; y++)
                    if (image.GetPixel(x, y).G < 190)
                    {
                        if (x < item.Bounds.Left + layout.Icon.Left + layout.Icon.Width / 2) leftPixels++;
                        else rightPixels++;
                    }
            Check(leftPixels > 5 && rightPixels > 5, "actual tray renderer paints both halves of icon without clipping");
            item.Image?.Dispose();
        }
        foreach (int dpi in new[] { 96, 120, 144, 192 })
        {
            var normal = TrayMenuLayout.Calculate(420, (int)(38 * dpi / 96F), dpi);
            var header = TrayMenuLayout.Calculate(420, (int)(42 * dpi / 96F), dpi);
            Check(normal.Text.Left == header.Text.Left && normal.Icon.Left == header.Icon.Left, $"tray columns align at {dpi} DPI");
            Check(Math.Abs((normal.Icon.Top * 2 + normal.Icon.Height) - normal.Text.Height) <= 1,
                $"tray icon and text share vertical center at {dpi} DPI");
            Check(normal.Icon.Right < normal.Text.Left && normal.Text.Right <= 420, $"tray spacing stays inside bounds at {dpi} DPI");
        }
    }

    internal static void RenderPreviews(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var language in LanguageHelper.SupportedLanguages)
        {
            using var service = new Ts6Service(65534, false);
            using var vm = new MainViewModel(new AppConfig { Language = language.Code }, service, false);
            var window = new SettingsWindow(vm);
            var root = (FrameworkElement)window.Content;
            root.Measure(new System.Windows.Size(960, 720));
            root.Arrange(new Rect(0, 0, 960, 720));
            root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(960, 720, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(directory, "settings-" + language.Code + ".png"))) encoder.Save(stream);
            ((System.Windows.Controls.ScrollViewer)window.FindName("SettingsScroller")).ScrollToBottom();
            root.UpdateLayout();
            var diagnostics = new RenderTargetBitmap(960, 720, 96, 96, PixelFormats.Pbgra32);
            diagnostics.Render(root);
            var diagnosticsEncoder = new PngBitmapEncoder();
            diagnosticsEncoder.Frames.Add(BitmapFrame.Create(diagnostics));
            using (var stream = File.Create(Path.Combine(directory, "diagnostics-" + language.Code + ".png"))) diagnosticsEncoder.Save(stream);
            window.Close();

            using var menu = TrayIconHelper.CreateMenuShell();
            menu.CreateControl();
            menu.Items.Add(TrayIconHelper.CreateMenuItem("TS6 Speaker Overlay", TrayIconHelper.MenuGlyph.Status));
            menu.Items[0].Tag = "header";
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            string[] menuKeys = ["Lang_Tray_Settings", "", "Lang_Update_Check", "Lang_Tray_Refresh", "Lang_Tray_OpenLogs",
                "Lang_Tray_Hide", "Lang_Tray_Unlock", "Lang_Tray_Exit"];
            TrayIconHelper.MenuGlyph[] glyphs = [TrayIconHelper.MenuGlyph.Settings, TrayIconHelper.MenuGlyph.Language,
                TrayIconHelper.MenuGlyph.Update, TrayIconHelper.MenuGlyph.Refresh, TrayIconHelper.MenuGlyph.Logs,
                TrayIconHelper.MenuGlyph.Window, TrayIconHelper.MenuGlyph.Unlock, TrayIconHelper.MenuGlyph.Exit];
            for (int i = 0; i < menuKeys.Length; i++)
                menu.Items.Add(TrayIconHelper.CreateMenuItem(menuKeys[i] == "" ? "Language / 语言" : LanguageHelper.GetString(menuKeys[i]), glyphs[i], i == menuKeys.Length - 1));
            TrayIconHelper.LayoutMenu(menu);
            using var image = RenderMenu(menu);
            image.Save(Path.Combine(directory, "tray-" + language.Code + ".png"));
            foreach (System.Windows.Forms.ToolStripItem item in menu.Items) item.Image?.Dispose();
            Console.WriteLine("Preview: " + language.Code);
        }
    }

    private static System.Drawing.Bitmap RenderMenu(System.Windows.Forms.ContextMenuStrip menu)
    {
        // DrawToBitmap on an unopened native popup can retain an unrelated HDC
        // clipping region. Exercise the real renderer on an explicit canvas instead.
        var image = new System.Drawing.Bitmap(menu.Width, menu.Height);
        image.SetResolution(menu.DeviceDpi, menu.DeviceDpi);
        using var graphics = System.Drawing.Graphics.FromImage(image);
        graphics.Clear(menu.BackColor);
        menu.Renderer.DrawToolStripBorder(new System.Windows.Forms.ToolStripRenderEventArgs(graphics, menu));
        foreach (System.Windows.Forms.ToolStripItem item in menu.Items)
        {
            if (!item.Available) continue;
            var state = graphics.Save();
            graphics.TranslateTransform(item.Bounds.Left, item.Bounds.Top);
            graphics.SetClip(new System.Drawing.Rectangle(0, 0, item.Width, item.Height));
            if (item is System.Windows.Forms.ToolStripSeparator separator)
                menu.Renderer.DrawSeparator(new System.Windows.Forms.ToolStripSeparatorRenderEventArgs(graphics, separator, false));
            else
            {
                menu.Renderer.DrawMenuItemBackground(new System.Windows.Forms.ToolStripItemRenderEventArgs(graphics, item));
                menu.Renderer.DrawItemText(new System.Windows.Forms.ToolStripItemTextRenderEventArgs(graphics, item, item.Text,
                    new System.Drawing.Rectangle(0, 0, item.Width, item.Height), item.ForeColor, item.Font,
                    System.Drawing.ContentAlignment.MiddleLeft));
            }
            graphics.Restore(state);
        }
        return image;
    }
}
