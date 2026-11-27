using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace CF.App.Rules.Page;

/// <summary>
/// Puts a tooltip outside the whole condition or path token, allowing for the room above it in the sheet.
/// </summary>
internal static class TipPlacement
{
    private const double _gap = 8;

    private static readonly FuncValueConverter<Size, double> _maxTipWidth = new FuncValueConverter<Size, double>(size => Math.Min(360, WithMargins(size.Width)));
    private static readonly FuncValueConverter<Size, double> _maxTipHeight = new FuncValueConverter<Size, double>(size => WithMargins(size.Height));

    public static readonly AttachedProperty<bool> AvoidsHeadProperty = AvaloniaProperty.RegisterAttached<Control, bool>("AvoidsHead", typeof(TipPlacement));

    static TipPlacement()
    {
        AvoidsHeadProperty.Changed.AddClassHandler<Control>((token, args) =>
        {
            if(args.NewValue is true)
            {
                ToolTip.AddToolTipOpeningHandler(token, FitTip);
                ToolTip.SetCustomPopupPlacementCallback(token, Place);
                ToolTip.SetPlacement(token, PlacementMode.Custom);
            }
            else
            {
                ToolTip.RemoveToolTipOpeningHandler(token, FitTip);
                token.ClearValue(ToolTip.PlacementProperty);
                token.ClearValue(ToolTip.CustomPopupPlacementCallbackProperty);
            }
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

    private static void FitTip(object? sender, CancelRoutedEventArgs args)
    {
        if(sender is not Control token || TopLevel.GetTopLevel(token) is not { } window)
            return;

        if(ToolTip.GetTip(token) is string text)
        {
            var tip = new ToolTip
            {
                Content = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }
                }
            };
            tip.Bind(Layoutable.MaxWidthProperty, new Binding(nameof(TopLevel.ClientSize)) { Source = window, Converter = _maxTipWidth });
            tip.Bind(Layoutable.MaxHeightProperty, new Binding(nameof(TopLevel.ClientSize)) { Source = window, Converter = _maxTipHeight });
            ToolTip.SetTip(token, tip);
        }
        else if(ToolTip.GetTip(token) is ToolTip tip)
        {
            tip.SetCurrentValue(Layoutable.MaxWidthProperty, Math.Min(360, WithMargins(window.ClientSize.Width)));
            tip.SetCurrentValue(Layoutable.MaxHeightProperty, WithMargins(window.ClientSize.Height));
        }
    }

    private static void Place(CustomPopupPlacement placement)
    {
        var token = placement.Target;
        var piece = token.FindAncestorOfType<ConditionPiece>() ?? token;
        var window = TopLevel.GetTopLevel(token);
        Visual? viewport = token.FindAncestorOfType<ScrollViewer>();
        viewport ??= window;
        // Avalonia supplies the token's anchor in window coordinates. Use the whole piece's height.
        var anchor = placement.AnchorRectangle;
        if(window != null && piece.TranslatePoint(default, window) is { } origin)
            placement.AnchorRectangle = new Rect(anchor.X, origin.Y, anchor.Width, piece.Bounds.Height);

        var size = placement.PopupSize;
        var width = window?.ClientSize.Width ?? double.PositiveInfinity;
        var height = window?.ClientSize.Height ?? double.PositiveInfinity;
        var bounds = placement.AnchorRectangle;
        var top = viewport == null ? bounds.Y : piece.TranslatePoint(default, viewport)?.Y ?? bounds.Y;
        var roomAbove = top - _gap;
        var roomBelow = height - bounds.Bottom - _gap;
        var below = roomAbove < size.Height && (roomBelow >= size.Height || roomBelow >= bounds.Y - _gap);
        var wantedX = bounds.Center.X - size.Width / 2;
        var wantedY = below ? bounds.Bottom + _gap : bounds.Y - size.Height - _gap;

        placement.Anchor = below ? PopupAnchor.Bottom : PopupAnchor.Top;
        placement.Gravity = below ? PopupGravity.Bottom : PopupGravity.Top;
        placement.Offset = new Point(FitOffset(wantedX, size.Width, width), (below ? _gap : -_gap) + FitOffset(wantedY, size.Height, height));
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX;
    }

    private static double WithMargins(double length)
    {
        return Math.Max(0, length - 2 * _gap);
    }

    private static double FitOffset(double start, double length, double available)
    {
        var lastStart = Math.Max(0, available - length);
        var margin = Math.Min(_gap, lastStart / 2);
        return Math.Clamp(start, margin, lastStart - margin) - start;
    }
}
