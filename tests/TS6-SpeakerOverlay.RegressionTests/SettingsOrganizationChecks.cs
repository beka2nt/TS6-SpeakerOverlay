using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Services;
using TS6_SpeakerOverlay.ViewModels;
using TS6_SpeakerOverlay.Views;
using Size = System.Windows.Size;
using RadioButton = System.Windows.Controls.RadioButton;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;

internal static class SettingsOrganizationChecks
{
    internal static readonly string[] Categories = ["Display", "Position", "Notifications", "Connection", "Application"];
    private static readonly string[][] ExpectedBindings =
    [
        ["Config.AvatarDisplayMode", "Config.ShowOnlyTalking", "Config.FontSize", "Config.AvatarSize", "Config.BackgroundOpacity"],
        ["Config.UserListAlignment", "Config.ScreenEdgeGap", "Config.SnapToScreenEdges", "Config.ItemSpacing", "Config.UiScale"],
        ["Config.EnableNotifications", "Config.EnableServerNotifications", "Config.EnableChannelNotifications"],
        ["SelectedServer", "Config.RemoteAppPort"],
        ["Config.AutoStart", "Config.AutoOpenWithTeamSpeak", "Config.CheckUpdatesOnStartup"]
    ];
    private static int _checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS: " + name);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(parent))
            if (child is DependencyObject node)
            {
                yield return node;
                foreach (var descendant in Descendants(node)) yield return descendant;
            }
    }

    private static bool BoundTo(DependencyObject node, DependencyProperty property, string path) =>
        BindingOperations.GetBinding(node, property)?.Path.Path == path;

    private static readonly DependencyProperty[] BoundProperties =
        [ToggleButton.IsCheckedProperty, Selector.SelectedValueProperty, Selector.SelectedItemProperty, RangeBase.ValueProperty];

    private static T BoundControl<T>(SettingsWindow window, string category, string path) where T : DependencyObject =>
        Descendants((DependencyObject)window.FindName(category + "Settings")).OfType<T>()
            .Single(node => BoundProperties.Any(property => BoundTo(node, property, path)));

    private static void Arrange(SettingsWindow window, double width = 960, double height = 720)
    {
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    internal static void Select(SettingsWindow window, string category)
    {
        ((RadioButton)window.FindName("Nav" + category)).IsChecked = true;
        Arrange(window);
    }

    internal static int Run()
    {
        foreach (var language in LanguageHelper.SupportedLanguages)
        {
            using var service = new Ts6Service(65534, false);
            using var vm = new MainViewModel(new AppConfig { Language = language.Code }, service, false);
            var window = new SettingsWindow(vm);
            Arrange(window);
            Check(((RadioButton)window.FindName("NavDisplay")).IsChecked == true, language.Code + " opens appearance by default");
            string configBefore = JsonSerializer.Serialize(vm.Config);
            foreach (string category in Categories)
            {
                Select(window, category);
                var scrollers = Categories.Select(name => (ScrollViewer)window.FindName(name == "Display" ? "SettingsScroller" : name + "Scroller")).ToArray();
                Check(scrollers.Count(scroller => scroller.Visibility == Visibility.Visible) == 1
                    && scrollers[Array.IndexOf(Categories, category)].Visibility == Visibility.Visible,
                    language.Code + " shows only " + category + " controls");
                var nav = (RadioButton)window.FindName("Nav" + category);
                var label = new TextBlock { Text = (string)nav.Content, FontFamily = nav.FontFamily, FontSize = nav.FontSize, FontWeight = nav.FontWeight };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Check(label.DesiredSize.Width <= nav.ActualWidth - 18, language.Code + " " + category + " navigation label fits");
                foreach (string path in ExpectedBindings[Array.IndexOf(Categories, category)])
                {
                    var controls = Descendants((DependencyObject)window.FindName(category + "Settings"));
                    Check(controls.Count(node => BoundProperties.Any(property => BoundTo(node, property, path))) == 1,
                        language.Code + " " + path + " has exactly one control in its category");
                }
                var selector = (ComboBox)window.FindName("LanguageSelector");
                Check(selector.Visibility == Visibility.Visible && selector.TransformToAncestor((FrameworkElement)window.Content).Transform(new System.Windows.Point()).Y < 146,
                    language.Code + " language remains above " + category);
            }
            Check(JsonSerializer.Serialize(vm.Config) == configBefore, language.Code + " category navigation preserves all preferences");
            Select(window, "Connection");
            ((Button)window.FindName("ConnectionRefreshButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(vm.ConnectionStatus == LanguageHelper.GetString("Lang_Status_RemoteUnavailable"), language.Code + " connection refresh action remains wired");
            Select(window, "Notifications");
            vm.Config.EnableNotifications = false;
            Arrange(window);
            Check(!BoundControl<CheckBox>(window, "Notifications", "Config.EnableServerNotifications").IsEnabled
                && !BoundControl<CheckBox>(window, "Notifications", "Config.EnableChannelNotifications").IsEnabled,
                language.Code + " notification master switch disables both child options");
            Select(window, "Display");
            var swatch = Descendants((DependencyObject)window.FindName("DisplaySettings")).OfType<Button>().Single(button => (string?)button.Tag == "#58BEFF");
            swatch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(vm.Config.TalkingColor == "#58BEFF", language.Code + " talking color presets remain interactive");
            BoundControl<ComboBox>(window, "Display", "Config.AvatarDisplayMode").SelectedValue = AvatarMode.AvatarOnly;
            BoundControl<Slider>(window, "Display", "Config.FontSize").Value = 20;
            Select(window, "Application");
            Select(window, "Display");
            Check(vm.Config.AvatarDisplayMode == AvatarMode.AvatarOnly && vm.Config.FontSize == 20,
                language.Code + " category switches retain live appearance changes");
            var appearanceScroller = (ScrollViewer)window.FindName("SettingsScroller");
            appearanceScroller.ScrollToBottom();
            Arrange(window);
            double savedOffset = appearanceScroller.VerticalOffset;
            Select(window, "Application");
            ((ScrollViewer)window.FindName("ApplicationScroller")).ScrollToBottom();
            Arrange(window);
            Select(window, "Display");
            Check(savedOffset > 0 && appearanceScroller.VerticalOffset == savedOffset,
                language.Code + " categories retain independent scroll positions");
            Select(window, "Application");
            LanguageHelper.SetLanguage(language.Code == "en-US" ? "de-DE" : "en-US");
            Arrange(window);
            Check(((RadioButton)window.FindName("NavApplication")).IsChecked == true,
                language.Code + " language switching preserves selected category");
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
            using var service = new Ts6Service(65534, false);
            using var vm = new MainViewModel(new AppConfig { Language = language.Code }, service, false);
            var window = new SettingsWindow(vm);
            foreach (string category in Categories)
            {
                Select(window, category);
                Save(window, Path.Combine(directory, language.Code + "-" + category + ".png"));
                if (category == "Display" || category == "Application")
                {
                    ((ScrollViewer)window.FindName(category == "Display" ? "SettingsScroller" : "ApplicationScroller")).ScrollToBottom();
                    Arrange(window);
                    Save(window, Path.Combine(directory, language.Code + "-" + category + "-bottom.png"));
                }
            }
            window.Close();
        }
    }

    private static void Save(SettingsWindow window, string path)
    {
        var bitmap = new RenderTargetBitmap(960, 720, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render((FrameworkElement)window.Content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
