using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CF.App.Rules;

namespace CF.App.Shell;

/// <summary>Gives every tooltip the same timing and edge clearance, including tooltips reached by keyboard.</summary>
internal sealed class ToolTipBehavior : AvaloniaObject
{
    public static readonly AttachedProperty<bool> RowFocusProperty = AvaloniaProperty.RegisterAttached<ToolTipBehavior, Control, bool>("RowFocus");
    public static readonly AttachedProperty<bool> LineLinkProperty = AvaloniaProperty.RegisterAttached<ToolTipBehavior, Control, bool>("LineLink");
    public static readonly AttachedProperty<Control?> LineWordsProperty = AvaloniaProperty.RegisterAttached<ToolTipBehavior, Control, Control?>("LineWords");

    private static readonly ConditionalWeakTable<TopLevel, TipState> _windows = new ConditionalWeakTable<TopLevel, TipState>();

    static ToolTipBehavior()
    {
        LineLinkProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            if(change.NewValue is true)
                ToolTip.AddToolTipOpeningHandler(control, FitLineTip);
            else
                ToolTip.RemoveToolTipOpeningHandler(control, FitLineTip);
        });
        ToolTip.TipProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if(ToolTip.GetTip(control) == null)
                return;

            if(ToolTip.GetPlacement(control) == PlacementMode.Custom)
                return;

            ToolTip.SetCustomPopupPlacementCallback(control, Place);
            ToolTip.SetPlacement(control, PlacementMode.Custom);
        });
        ToolTip.IsOpenProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            if(change.NewValue is true && TopLevel.GetTopLevel(control) is { } window)
                _windows.GetOrCreateValue(window).Open = control;
        });
        InputElement.GotFocusEvent.AddClassHandler<Control>((control, args) =>
        {
            if(ReferenceEquals(control, args.Source) && args.NavigationMethod != NavigationMethod.Pointer)
                StartFocus(control);
        });
        InputElement.LostFocusEvent.AddClassHandler<Control>((control, args) =>
        {
            if(ReferenceEquals(control, args.Source))
                EndFocus(control);
        });
        RowFocusProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            if(change.NewValue is true)
                StartFocus(control);
            else
                EndFocus(control);
        });
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((window, args) =>
        {
            if(args.Key == Key.Escape && _windows.TryGetValue(window, out var state))
                args.Handled = state.Dismiss();
        }, RoutingStrategies.Tunnel);
    }

    public static void Initialize()
    {
        // Register the app-wide defaults before any controls are shown.
    }

    public static bool GetRowFocus(Control control) { return control.GetValue(RowFocusProperty); }
    public static void SetRowFocus(Control control, bool value) { control.SetValue(RowFocusProperty, value); }
    public static bool GetLineLink(Control control) { return control.GetValue(LineLinkProperty); }
    public static void SetLineLink(Control control, bool value) { control.SetValue(LineLinkProperty, value); }
    public static Control? GetLineWords(Control control) { return control.GetValue(LineWordsProperty); }
    public static void SetLineWords(Control control, Control? value) { control.SetValue(LineWordsProperty, value); }

    private static void StartFocus(Control control)
    {
        if(ToolTip.GetTip(control) != null && TopLevel.GetTopLevel(control) is { } window)
            _windows.GetOrCreateValue(window).Focus(control);
    }

    private static void EndFocus(Control control)
    {
        if(TopLevel.GetTopLevel(control) is { } window && _windows.TryGetValue(window, out var state))
            state.Blur(control);
    }

    private static void Place(CustomPopupPlacement placement)
    {
        if(placement.Target is not Control target)
            return;

        var window = TopLevel.GetTopLevel(target);
        var bounds = placement.AnchorRectangle;
        var size = placement.PopupSize;
        var gap = GetRowFocus(target) || target.IsKeyboardFocusWithin ? 12 : 8;
        if(window == null)
            return;

        var limit = window.ClientSize;
        if(GetLineLink(target))
        {
            var area = LineTipArea(target, gap);
            placement.Anchor = PopupAnchor.TopLeft;
            placement.Gravity = PopupGravity.BottomLeft;
            placement.Offset = new Point(area.Right, Math.Clamp(0, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.None;
            return;
        }
        else
        {
            var below = bounds.Bottom + gap + size.Height <= limit.Height - 8 || bounds.Y < limit.Height - bounds.Bottom;
            placement.Anchor = below ? PopupAnchor.BottomLeft : PopupAnchor.TopLeft;
            placement.Gravity = below ? PopupGravity.BottomRight : PopupGravity.TopRight;
            placement.Offset = new Point(Fit(bounds.X, size.Width, limit.Width), below ? gap : -gap);
        }

        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY;
    }

    private static void FitLineTip(object? sender, CancelRoutedEventArgs args)
    {
        if(sender is not Control target || ToolTip.GetTip(target) is not ToolTip tip)
            return;

        var gap = GetRowFocus(target) || target.IsKeyboardFocusWithin ? 12 : 8;
        var area = LineTipArea(target, gap);
        if(area.Width < 1 || area.Height < 1)
        {
            args.Cancel = true;
            return;
        }

        var room = LineTipRoom(target, -area.Right);
        tip.MaxWidth = Math.Min(area.Width, room >= 160 ? Math.Min(room, 280) : 280);
        tip.MaxHeight = area.Height;
    }

    private static Rect LineTipArea(Control target, double gap)
    {
        var viewport = target.FindAncestorOfType<ScrollContentPresenter>();
        if(viewport == null || target.TranslatePoint(default, viewport) is not { } origin)
            return default;

        // Keep the entire link column clear, including links wider than the one being hovered.
        var row = target.FindAncestorOfType<Grid>()!;
        var linkColumn = row.ColumnDefinitions[Grid.GetColumn(target)].ActualWidth;
        var columnLeft = origin.X + target.Bounds.Width + target.Margin.Right - linkColumn;
        var right = Math.Min(origin.X - gap, columnLeft);
        var rules = target.FindAncestorOfType<RulesView>()!;
        var (top, bottom) = rules.LineTipVerticalBounds(viewport, origin.Y, viewport.Bounds.Height);
        return new Rect(8 - origin.X, top - origin.Y, Math.Max(0, right - 8), Math.Max(0, bottom - top));
    }

    private static double LineTipRoom(Control target, double gap)
    {
        if(GetLineWords(target) is Grid words)
        {
            var right = words.Children.Where(child => child.IsVisible)
                .Max(child => child.TranslatePoint(new Point(child.Bounds.Width, 0), target)?.X ?? 0);
            return -right - gap - 8;
        }

        var row = target.FindAncestorOfType<Grid>();
        if(row == null)
            return 0;

        var title = row.Children.OfType<TextBlock>().First(child => Grid.GetColumn(child) == 1);
        var rightOfTitle = title.TranslatePoint(new Point(title.TextLayout.Width, 0), target)?.X ?? 0;
        return -rightOfTitle - gap - 8;
    }

    private static double Fit(double start, double length, double limit)
    {
        return Math.Clamp(start, 8, Math.Max(8, limit - length - 8)) - start;
    }

    private sealed class TipState
    {
        private IDisposable? _wait;
        private Control? _focused;

        public Control? Open { get; set; }

        public void Focus(Control control)
        {
            _wait?.Dispose();
            _focused = control;
            _wait = DispatcherTimer.RunOnce(() =>
            {
                if(control.IsEffectivelyVisible && (control.IsFocused || GetRowFocus(control)))
                    ToolTip.SetIsOpen(control, true);
            }, TimeSpan.FromMilliseconds(ToolTip.GetShowDelay(control)));
        }

        public void Blur(Control control)
        {
            if(ReferenceEquals(control, _focused))
            {
                _wait?.Dispose();
                _focused = null;
                ToolTip.SetIsOpen(control, false);
            }
        }

        public bool Dismiss()
        {
            _wait?.Dispose();
            _focused = null;
            if(Open == null || !ToolTip.GetIsOpen(Open))
                return false;

            ToolTip.SetIsOpen(Open, false);
            Open = null;
            return true;
        }
    }
}
