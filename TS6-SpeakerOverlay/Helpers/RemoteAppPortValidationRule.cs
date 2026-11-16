using System.Globalization;
using System.Windows.Controls;

namespace TS6_SpeakerOverlay.Helpers;

public sealed class RemoteAppPortValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
        int.TryParse(value?.ToString(), NumberStyles.Integer, cultureInfo, out int port) && port is >= 1 and <= 65535
            ? ValidationResult.ValidResult : new ValidationResult(false, LanguageHelper.GetString("Lang_Port_Range"));
}
