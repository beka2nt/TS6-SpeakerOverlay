using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Services;
using TS6_SpeakerOverlay.ViewModels;
using TS6_SpeakerOverlay.Views;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;

internal static class UtilityPolishChecks
{
    private static int _checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS: " + name);
    }

    private static void Arrange(Window window, double width, double height)
    {
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte channel)
        {
            double value = channel / 255D;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }

    private static double Contrast(Color first, Color second)
    {
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    internal static string[] FixtureEntries(bool errorsOnly) => errorsOnly
        ? ["2026-11-11T12:01:04+08:00 [ERROR] Fixture: connection check timed out"]
        : ["2026-11-11T12:01:04+08:00 [ERROR] Fixture: connection check timed out", "2026-11-11T12:00:00+08:00 [INFO] Fixture: overlay started"];

    internal static int Run()
    {
        foreach (var language in LanguageHelper.SupportedLanguages)
        {
            using var service = new Ts6Service(65534, false);
            using var vm = new MainViewModel(new AppConfig { Language = language.Code }, service, false);
            var settings = new SettingsWindow(vm);
            var logs = new LogViewerWindow(errorsOnly => FixtureEntries(errorsOnly));
            Arrange(settings, 960, 720);
            foreach (string key in new[] { "SurfaceBrush", "TextBrush", "MutedBrush", "AccentBrush", "BorderBrush" })
                Check(((SolidColorBrush)settings.FindResource(key)).Color == ((SolidColorBrush)logs.FindResource(key)).Color,
                    language.Code + " settings/logs share " + key);
            var primary = new Button { Style = (Style)settings.FindResource("PrimaryButton") };
            var background = ((SolidColorBrush)primary.Background).Color;
            var foreground = ((SolidColorBrush)primary.Foreground).Color;
            Check(Contrast(background, foreground) >= 4.5, language.Code + " primary button text contrast meets 4.5:1");
            Check(Contrast(((SolidColorBrush)settings.FindResource("MutedBrush")).Color, Colors.White) >= 4.5,
                language.Code + " secondary text remains readable on white");
            var quiet = new Button { Style = (Style)settings.FindResource("QuietButton") };
            Check(primary.Height == 36 && quiet.Height == 36 && logs.AllButton.Height == 36,
                language.Code + " utility windows use consistent 36px buttons");
            var scroller = (ScrollViewer)settings.FindName("SettingsScroller");
            Check(scroller.TransformToAncestor((FrameworkElement)settings.Content).Transform(new System.Windows.Point()).Y <= 170,
                language.Code + " compact header leaves more settings visible");
            foreach (var dimensions in new[] { new Size(860, 610), new Size(680, 460) })
            {
                Arrange(logs, dimensions.Width, dimensions.Height);
                var actions = (WrapPanel)logs.FindName("LogActions");
                Check(actions.Children.Cast<Button>().All(button =>
                {
                    var position = button.TransformToAncestor(actions).Transform(new System.Windows.Point());
                    return position.X >= 0 && position.X + button.ActualWidth <= actions.ActualWidth + 0.5
                        && position.Y + button.ActualHeight <= actions.ActualHeight + 0.5;
                }), language.Code + " log actions wrap without clipping at " + dimensions.Width);
                Check(logs.LogTextBox.ActualHeight > 100 && logs.LogTextBox.IsReadOnly,
                    language.Code + " log content stays readable at " + dimensions.Width);
            }
            Check(logs.LogTextBox.Text.Contains("[INFO]") && logs.LogTextBox.Text.Contains("[ERROR]"),
                language.Code + " logs initially show all fixture entries");
            logs.ErrorsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!logs.LogTextBox.Text.Contains("[INFO]") && logs.LogTextBox.Text.Contains("[ERROR]"),
                language.Code + " error filtering survives visual polish");
            Check(((SolidColorBrush)logs.ErrorsButton.BorderBrush).Color == ((SolidColorBrush)logs.FindResource("AccentBrush")).Color,
                language.Code + " selected log filter has explicit accent outline");
            logs.AllButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(logs.LogTextBox.Text.Contains("[INFO]"), language.Code + " all-entries filter remains interactive");
            logs.Close();
            settings.Close();
        }
        LanguageHelper.SetLanguage("en-US");
        return _checks;
    }
}
