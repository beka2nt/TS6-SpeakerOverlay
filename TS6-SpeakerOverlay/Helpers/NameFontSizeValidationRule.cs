using System.Globalization;
using System.Windows.Controls;

namespace TS6_SpeakerOverlay.Helpers;

public sealed class NameFontSizeValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
        double.TryParse(value?.ToString(), NumberStyles.Float, cultureInfo, out double size)
        && double.IsFinite(size) && size is >= 10 and <= 36
            ? ValidationResult.ValidResult
            : new ValidationResult(false, LanguageHelper.GetString("Lang_NameFont_Range"));
}
