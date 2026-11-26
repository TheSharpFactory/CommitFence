using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CF.App.Rules.Page;

/// <summary>
/// Keeps a picker under its token and inside the window. It opens 6 under the token, never above it. A token that is not fully in view
/// is first scrolled to 16 under the head. When the room below is too short,
/// the sheet is scrolled up further, as far as the sheet can go, until the top of the clause that holds the token is 16 under the head (a
/// row is never half-cut). When the sheet has come to its end and the picker still has no room, the sheet gets the missing room as space
/// at its end for as long as the picker is open, and is scrolled again. What still does not fit scrolls inside the picker, which is never
/// taller than the room left below its token, 8 short of the window's bottom edge. Sideways it
/// keeps 8 from the window's edges, moving left. A picker is never wider than the room it has: its least width (340 for most) gives way
/// when the window is narrower. While the picker is open it fits again when the window is resized.
/// </summary>
internal static class PickerRoom
{
    private const double _under = 6;
    private const double _edge = 8;
    private const double _clauseFromTop = 16;

    public static readonly AttachedProperty<bool> FitsProperty = AvaloniaProperty.RegisterAttached<Popup, bool>("Fits", typeof(PickerRoom));

    /// <summary>
    /// The width a picker's surface has at the least when the window has room; it is held back to the room there is.
    /// </summary>
    public static readonly AttachedProperty<double> LeastProperty = AvaloniaProperty.RegisterAttached<Border, double>("Least", typeof(PickerRoom));

    /// <summary>
    /// The margin the content of the sheet had before it was given room at its end.
    /// </summary>
    private static readonly AttachedProperty<Thickness?> _keptMarginProperty = AvaloniaProperty.RegisterAttached<Control, Thickness?>("KeptMargin", typeof(PickerRoom));

    static PickerRoom()
    {
        LeastProperty.Changed.AddClassHandler<Border>((surface, args) => surface.MinWidth = (double)args.NewValue!);

        FitsProperty.Changed.AddClassHandler<Popup>((popup, args) =>
        {
            if(args.NewValue is true)
                popup.Opened += OnOpened;
            else
                popup.Opened -= OnOpened;
        });
    }

    public static double GetLeast(Border surface)
    {
        return surface.GetValue(LeastProperty);
    }

    public static void SetLeast(Border surface, double value)
    {
        surface.SetValue(LeastProperty, value);
    }

    public static bool GetFits(Popup popup)
    {
        return popup.GetValue(FitsProperty);
    }

    public static void SetFits(Popup popup, bool value)
    {
        popup.SetValue(FitsProperty, value);
    }

    private static void OnOpened(object? sender, EventArgs e)
    {
        if(sender is not Popup { PlacementTarget: Control target } popup || TopLevel.GetTopLevel(target) is not { } window)
            return;

        Dispatcher.UIThread.Post(() => Fit(popup), DispatcherPriority.Loaded);
        FitAgainOnResize(popup, window);
    }

    private static void FitAgainOnResize(Popup popup, TopLevel window)
    {
        void OnWindowChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if(args.Property == Visual.BoundsProperty)
                Dispatcher.UIThread.Post(() => Fit(popup), DispatcherPriority.Loaded);
        }

        void OnClosed(object? sender, EventArgs args)
        {
            if(popup.PlacementTarget?.FindAncestorOfType<ScrollViewer>() is { } scroller)
                ReleaseRoom(scroller);

            window.PropertyChanged -= OnWindowChanged;
            popup.Closed -= OnClosed;
        }

        window.PropertyChanged += OnWindowChanged;
        popup.Closed += OnClosed;
    }

    private static void Fit(Popup popup)
    {
        if(!popup.IsOpen || popup is not { PlacementTarget: Control target, Child: Control child } || TopLevel.GetTopLevel(target) is not { } window)
            return;

        child.ClearValue(Control.MaxHeightProperty);
        var available = Math.Max(0, window.Bounds.Width - (2 * _edge));
        child.MaxWidth = available;
        foreach(var surface in child.GetVisualDescendants().OfType<Border>().Prepend(child as Border).OfType<Border>().Where(candidate => candidate.Classes.Contains("picker")))
        {
            surface.MaxWidth = available;
            surface.MinWidth = Math.Min(GetLeast(surface), available);
        }

        child.Measure(Size.Infinity);
        KeepFromTheSides(popup, target, child, window);
        var wanted = child.DesiredSize.Height;
        var scroller = target.FindAncestorOfType<ScrollViewer>();
        if(scroller != null)
        {
            ReleaseRoom(scroller);
            ScrollIntoView(scroller, target);
        }

        var room = RoomBelow(target, window);
        if(wanted > room && scroller != null)
        {
            room += ScrollUp(scroller, target);
            if(wanted > room)
            {
                GiveRoom(scroller, wanted - room);
                room += ScrollUp(scroller, target);
            }
        }

        if(wanted > room)
            child.MaxHeight = Math.Max(0, room);

        Nudge(popup);
    }

    /// <summary>
    /// The picker's left edge stands in line with its token's; when the picker would come closer than 8 to the window's edge it moves
    /// left (or right) just so far.
    /// </summary>
    private static void KeepFromTheSides(Popup popup, Control target, Control child, TopLevel window)
    {
        var left = target.TranslatePoint(default, window)?.X ?? 0;
        var wide = child.DesiredSize.Width;
        var offset = 0.0;
        if(left + wide > window.Bounds.Width - _edge)
            offset = window.Bounds.Width - _edge - wide - left;

        popup.HorizontalOffset = Math.Max(offset, _edge - left);
    }

    private static double RoomBelow(Control target, TopLevel window)
    {
        var bottom = target.TranslatePoint(new Point(0, target.Bounds.Height), window)?.Y ?? 0;
        return window.Bounds.Height - _edge - bottom - _under;
    }

    /// <summary>
    /// A token that is not fully in view is scrolled to 16 under the head, by at most what the sheet can give, before its picker opens.
    /// </summary>
    private static void ScrollIntoView(ScrollViewer scroller, Control target)
    {
        var top = target.TranslatePoint(default, scroller)?.Y ?? 0;
        if(top >= 0 && top + target.Bounds.Height <= scroller.Viewport.Height)
            return;

        var most = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        var offset = Math.Clamp(scroller.Offset.Y + top - _clauseFromTop, 0, most);
        scroller.Offset = new Vector(scroller.Offset.X, offset);
        scroller.UpdateLayout();
    }

    /// <summary>
    /// Scrolls the sheet so that the clause of the token stands 16 under the head, by at most what the sheet can give, and answers how
    /// far it went.
    /// </summary>
    private static double ScrollUp(ScrollViewer scroller, Control target)
    {
        var anchor = target.GetVisualAncestors().OfType<Grid>().FirstOrDefault(candidate => candidate.Classes.Contains("clause")) ?? target;
        var anchorTop = anchor.TranslatePoint(default, scroller)?.Y ?? 0;
        var canScroll = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height - scroller.Offset.Y);
        var by = Math.Clamp(anchorTop - _clauseFromTop, 0, canScroll);
        if(by <= 0)
            return 0;

        scroller.Offset = new Vector(scroller.Offset.X, scroller.Offset.Y + by);
        scroller.UpdateLayout();
        return by;
    }

    /// <summary>
    /// Adds the room a picker still lacks as space at the end of the sheet, so that the sheet can scroll up as far as the picker needs;
    /// the space goes when the picker closes.
    /// </summary>
    private static void GiveRoom(ScrollViewer scroller, double missing)
    {
        if(scroller.Content is not Control content)
            return;

        var margin = content.Margin;
        content.SetValue(_keptMarginProperty, margin);
        content.Margin = new Thickness(margin.Left, margin.Top, margin.Right, margin.Bottom + missing);
        scroller.UpdateLayout();
    }

    private static void ReleaseRoom(ScrollViewer scroller)
    {
        if(scroller.Content is Control content && content.GetValue(_keptMarginProperty) is { } kept)
        {
            content.Margin = kept;
            content.ClearValue(_keptMarginProperty);
        }
    }

    /// <summary>
    /// The popup finds its place again from where its token stands now.
    /// </summary>
    private static void Nudge(Popup popup)
    {
        popup.VerticalOffset += 0.01;
        popup.VerticalOffset -= 0.01;
    }
}
