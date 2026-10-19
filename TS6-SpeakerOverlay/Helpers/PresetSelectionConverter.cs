using System.Globalization;
using System.Windows.Data;

namespace TS6_SpeakerOverlay.Helpers;

public sealed class PresetSelectionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is Models.OverlayPosition first && values[1] is Models.OverlayPosition second && first == second;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
