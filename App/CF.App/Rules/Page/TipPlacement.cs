using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace CF.App.Rules.Page;

/// <summary>
/// Puts a token's tooltip above its row, or below it when the row is so close to the head of the sheet that a tooltip above would
/// stand under the head.
/// </summary>
internal static class TipPlacement
{
    /// <summary>
    /// How far under the head a token must be for its tooltip to have room above it: the tooltip, a few lines of a long value, and the gap.
    /// </summary>
    private const double _roomAbove = 96;
    private const double _gap = 8;

    public static readonly AttachedProperty<bool> AvoidsHeadProperty = AvaloniaProperty.RegisterAttached<Control, bool>("AvoidsHead", typeof(TipPlacement));

    static TipPlacement()
    {
        AvoidsHeadProperty.Changed.AddClassHandler<Control>((token, args) =>
        {
            if(args.NewValue is true)
                token.PointerEntered += (_, _) => Place(token);
        });
    }

    public static bool GetAvoidsHead(Control token)
    {
        return token.GetValue(AvoidsHeadProperty);
    }

    public static void SetAvoidsHead(Control token, bool value)
    {
        token.SetValue(AvoidsHeadProperty, value);
    }

    private static void Place(Control token)
    {
        var top = token.FindAncestorOfType<ScrollViewer>() is { } scroller ? token.TranslatePoint(default, scroller)?.Y : null;
        var below = top is < _roomAbove;
        ToolTip.SetPlacement(token, below ? PlacementMode.Bottom : PlacementMode.Top);
        ToolTip.SetVerticalOffset(token, below ? _gap : -_gap);
    }
}
