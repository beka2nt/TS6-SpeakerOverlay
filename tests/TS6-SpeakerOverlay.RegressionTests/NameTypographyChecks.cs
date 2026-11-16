using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TS6_SpeakerOverlay;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Services;
using TS6_SpeakerOverlay.ViewModels;
using TS6_SpeakerOverlay.Views;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using Size = System.Windows.Size;

internal static class NameTypographyChecks
{
    private static int _checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS: " + name);
    }

    private static void Arrange(FrameworkElement root, double width, double height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        root.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
    }

    internal static int Run()
    {
        var config = JsonSerializer.Deserialize<AppConfig>("{\"FontSize\":17}")!;
        Check(config.NameFontFamily == AppConfig.DefaultNameFontFamily && config.FontSize == 17,
            "old configs preserve name size and acquire the existing font fallback");
        config.NameFontFamily = "  Consolas  ";
        config.FontSize = 22;
        var restored = JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(config))!;
        Check(restored.NameFontFamily == "Consolas" && restored.FontSize == 22, "name font and size survive config roundtrip");
        config.NameFontFamily = " ";
        Check(config.NameFontFamily == AppConfig.DefaultNameFontFamily, "blank name font uses the existing default");
        config.FontSize = double.NaN;
        Check(config.FontSize == 14, "invalid name size returns to 14");
        config.FontSize = 100;
        Check(config.FontSize == 36, "name size is bounded above");
        config.FontSize = 1;
        Check(config.FontSize == 10, "name size is bounded below");
        Check(OverlayFontCatalog.ForSelection("Fixture missing font").Any(font => font.FamilyName == "Fixture missing font"),
            "a saved missing font remains selectable instead of silently resetting");

        using var service = new Ts6Service(65534, false);
        using var vm = new MainViewModel(new AppConfig { Language = "en-US" }, service, false);
        var settings = new SettingsWindow(vm);
        var overlay = new MainWindow(vm);
        vm.Users.Add(new User { ClientId = 1, Name = "Font preview" });
        Arrange((FrameworkElement)settings.Content, 960, 720);
        Arrange((FrameworkElement)overlay.Content, 320, 600);
        var selector = (ComboBox)settings.FindName("NameFontSelector");
        var input = (TextBox)settings.FindName("NameFontSizeInput");
        string selectedFont = vm.NameFonts.FirstOrDefault(font => font.FamilyName == "Consolas")?.FamilyName
            ?? vm.NameFonts.First(font => font.FamilyName != AppConfig.DefaultNameFontFamily).FamilyName;
        selector.SelectedValue = selectedFont;
        input.Text = "22";
        input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Arrange((FrameworkElement)settings.Content, 960, 720);
        Arrange((FrameworkElement)overlay.Content, 320, 600);
        Check(selector.Items.Count > 1 && vm.Config.NameFontFamily == selectedFont, "installed font selector updates config");
        Check(vm.Config.FontSize == 22, "numeric name size input updates config");
        var preview = (TextBlock)settings.FindName("PreviewUserName");
        Check(preview.FontFamily.Source == selectedFont && preview.FontSize == 22, "settings name preview uses the selected font and size");
        var list = (ItemsControl)overlay.FindName("UserList");
        var presenter = (ContentPresenter)list.ItemContainerGenerator.ContainerFromIndex(0);
        var actualName = (TextBlock)list.ItemTemplate.FindName("UserName", presenter);
        Check(actualName.FontFamily.Source == selectedFont && actualName.FontSize == 22, "actual overlay member name uses the selected font and size");
        Check(overlay.FontFamily.Source == AppConfig.DefaultNameFontFamily, "name customization does not change overlay hints or status fonts");
        var searchInput = (TextBox)settings.FindName("NameFontSearchInput");
        int fullFontCount = selector.Items.Count;
        string fontBeforeSearch = vm.Config.NameFontFamily;
        searchInput.Text = " SeGoE ";
        searchInput.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Arrange((FrameworkElement)settings.Content, 960, 720);
        Check(vm.NameFontSearch == " SeGoE " && selector.Items.Count < fullFontCount,
            "font search input filters the existing dropdown case-insensitively");
        Check(selector.Items.Cast<OverlayFontOption>().All(font => font.FamilyName == fontBeforeSearch || OverlayFontCatalog.Matches(font, "segoe")),
            "filtered dropdown contains only matches and the preserved current font");
        Check(vm.Config.NameFontFamily == fontBeforeSearch && (string?)selector.SelectedValue == fontBeforeSearch,
            "searching for another font cannot clear or replace the active font");
        string matchingFont = vm.NameFonts.First(font => font.FamilyName != fontBeforeSearch
            && OverlayFontCatalog.Matches(font, "segoe")).FamilyName;
        selector.SelectedValue = matchingFont;
        Arrange((FrameworkElement)settings.Content, 960, 720);
        Check(vm.Config.NameFontFamily == matchingFont && preview.FontFamily.Source == matchingFont,
            "choosing a search result applies it to the live preview");
        vm.NameFontSearch = "fixture-font-that-does-not-exist-57831";
        Arrange((FrameworkElement)settings.Content, 960, 720);
        Check(vm.HasNoNameFontMatches && selector.Items.Count == 1
            && (string?)selector.SelectedValue == matchingFont && vm.Config.NameFontFamily == matchingFont,
            "no-match search shows a hint while retaining the active font");
        Check(!JsonSerializer.Serialize(vm.Config).Contains("fixture-font-that-does-not-exist-57831"),
            "search text is temporary UI state and is not saved to configuration");
        vm.ClearNameFontSearchCommand.Execute(null);
        Arrange((FrameworkElement)settings.Content, 960, 720);
        Check(vm.NameFontSearch == "" && !vm.HasNoNameFontMatches && selector.Items.Count == fullFontCount
            && (string?)selector.SelectedValue == matchingFont,
            "clear search restores the complete dropdown without changing the font");
        var bilingualFont = new OverlayFontOption("Microsoft YaHei", "微软雅黑")
            { SearchNames = "Microsoft YaHei 微软雅黑" };
        Check(OverlayFontCatalog.Matches(bilingualFont, "雅黑") && OverlayFontCatalog.Matches(bilingualFont, "yAhEi"),
            "font search matches localized Chinese aliases and English family names");
        input.Text = "not-a-number";
        input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Check(Validation.GetHasError(input) && vm.Config.FontSize == 22, "invalid numeric input is shown without overwriting the last size");
        input.Text = "999";
        input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Check(Validation.GetHasError(input) && vm.Config.FontSize == 22, "out-of-range size is rejected by the input validation");
        input.Text = "20";
        input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Check(!Validation.GetHasError(input) && vm.Config.FontSize == 20, "correcting numeric input clears validation");
        vm.NameFontSearch = "fixture-no-font-before-reset";
        Arrange((FrameworkElement)settings.Content, 960, 720);
        vm.Config.ResetDefaults();
        Arrange((FrameworkElement)settings.Content, 960, 720);
        Check(vm.Config.NameFontFamily == AppConfig.DefaultNameFontFamily && vm.Config.FontSize == 14
            && (string?)selector.SelectedValue == AppConfig.DefaultNameFontFamily, "reset restores name font, size and selection");
        settings.Close();
        overlay.Close();
        return _checks;
    }
}
