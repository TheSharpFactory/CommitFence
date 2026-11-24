using Avalonia.Data.Converters;

namespace CF.App.Rules;

/// <summary>
/// Where the menu of an effect stands against its row, so that its right edge is in line with the effect control's:
/// the control's right edge is 62 from the row's content edge, and 28 more in one pane, where the row ends with an arrow.
/// </summary>
internal static class MenuOffset
{
    private const double _twoPanes = -62;
    private const double _onePane = -90;

    public static readonly FuncValueConverter<bool, double> Horizontal = new FuncValueConverter<bool, double>(isOnePane => isOnePane ? _onePane : _twoPanes);
}
