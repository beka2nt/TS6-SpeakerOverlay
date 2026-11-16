using System.Globalization;
using System.Windows.Markup;
using System.Windows.Media;
using TS6_SpeakerOverlay.Models;

namespace TS6_SpeakerOverlay.Helpers;

public sealed record OverlayFontOption(string FamilyName, string DisplayName)
{
    public string SearchNames { get; init; } = FamilyName + " " + DisplayName;
}

internal static class OverlayFontCatalog
{
    private static readonly Lazy<IReadOnlyList<OverlayFontOption>> Installed = new(() =>
    {
        var language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        return Fonts.SystemFontFamilies
            .Select(font => new OverlayFontOption(font.Source,
                font.FamilyNames.TryGetValue(language, out string? localName) ? localName : font.Source)
            {
                SearchNames = string.Join(" ", font.FamilyNames.Values.Append(font.Source))
            })
            .DistinctBy(font => font.FamilyName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(font => font.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Prepend(new OverlayFontOption(AppConfig.DefaultNameFontFamily, "Microsoft YaHei UI / Segoe UI"))
            .ToArray();
    });

    internal static IReadOnlyList<OverlayFontOption> ForSelection(string familyName)
    {
        var fonts = Installed.Value;
        // Keep a saved choice visible after moving to a PC without that font;
        // WPF supplies its normal glyph fallback rather than clearing the choice.
        return fonts.Any(font => font.FamilyName == familyName) ? fonts
            : fonts.Prepend(new OverlayFontOption(familyName, familyName)).ToArray();
    }

    internal static bool Matches(OverlayFontOption font, string search) =>
        string.IsNullOrWhiteSpace(search)
        || font.SearchNames.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase);
}
