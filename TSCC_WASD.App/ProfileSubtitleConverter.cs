using System.Globalization;
using System.Windows.Data;
using TSCC_WASD.Core;

namespace TSCC_WASD.App;

/// <summary>
/// Second line of a profile in the list: the game's process name (or a note for the profile that has none),
/// plus "使用中" for the profile mapping is using. Values: match name, profile, selected profile, mapping running.
/// </summary>
public sealed class ProfileSubtitleConverter : IMultiValueConverter
{
    public static string Text(string? matchProcessName, bool active, bool zh)
    {
        string text = string.IsNullOrWhiteSpace(matchProcessName)
            ? (zh ? "沒有對應的遊戲時使用" : "Used when no game matches")
            : matchProcessName;
        return active ? text + (zh ? " · 使用中" : " · In use") : text;
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        string? match = values.Length > 0 ? values[0] as string : null;
        bool active = values.Length > 3 && values[3] is true && values[1] is not null && ReferenceEquals(values[1], values[2]);
        return Text(match, active, L.Chinese);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
