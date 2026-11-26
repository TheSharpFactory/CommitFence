using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CF.App.Rules.Page.Code;
using CF.Core.Review;

namespace CF.App.Dashboard;

/// <summary>One review mark, shared by the chart and its keys.</summary>
internal sealed class ReviewMark : Control
{
    private const double _stopHeight = 3;
    private const double _signWidth = 3;
    private const double _stopLeast = 12;
    private const double _exclamationLeast = 18;

    public static readonly StyledProperty<ReviewOutcome> KindProperty =
        AvaloniaProperty.Register<ReviewMark, ReviewOutcome>(nameof(Kind));

    static ReviewMark()
    {
        AffectsRender<ReviewMark>(KindProperty);
    }

    public ReviewMark()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public ReviewOutcome Kind
    {
        get { return GetValue(KindProperty); }
        set { SetValue(KindProperty, value); }
    }

    public override void Render(DrawingContext context)
    {
        Draw(context, new Rect(Bounds.Size), Kind, InkOf(this, Kind), true);
    }

    public static ReviewMarkInk InkOf(Control control, ReviewOutcome kind)
    {
        return kind switch
        {
            ReviewOutcome.Blocked => new ReviewMarkInk(ThemeBrushes.Find(control, "block"), ThemeBrushes.Find(control, "on-block")),
            ReviewOutcome.Incomplete => new ReviewMarkInk(ThemeBrushes.Find(control, "warn"), ThemeBrushes.Find(control, "on-warn")),
            ReviewOutcome.Clean or ReviewOutcome.Warned => new ReviewMarkInk(ThemeBrushes.Find(control, "data"), null),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    /// <summary>
    /// Draws a box or block of one kind with its mark: the stop bar once it is 12 high, the exclamation mark once it is 18 high.
    /// A key box (in the count line and the day line) always carries its mark, in its own proportions.
    /// </summary>
    public static void Draw(DrawingContext context, Rect box, ReviewOutcome kind, ReviewMarkInk ink, bool key = false)
    {
        var radius = key ? box.Height / 4 : Math.Min(4, box.Height / 2);
        context.DrawRectangle(ink.Fill, null, box, radius, radius);
        if(ink.Sign == null)
            return;

        if(kind == ReviewOutcome.Blocked && (key || box.Height >= _stopLeast))
            Stop(context, ink.Sign, box, key ? box.Width * 0.22 : 4);
        else if(kind == ReviewOutcome.Incomplete && (key || box.Height >= _exclamationLeast))
            Exclamation(context, ink.Sign, box, key);
    }

    private static void Stop(DrawingContext context, IBrush brush, Rect box, double inset)
    {
        Sign(context, brush, new Rect(box.X + inset, box.Y + ((box.Height - _stopHeight) / 2), box.Width - (2 * inset), _stopHeight));
    }

    private static void Exclamation(DrawingContext context, IBrush brush, Rect box, bool key)
    {
        var bar = key ? box.Height * 0.375 : 8;
        var gap = key ? 1.5 : 2;
        var top = box.Y + ((box.Height - (bar + gap + _signWidth)) / 2);
        var left = box.X + ((box.Width - _signWidth) / 2);
        Sign(context, brush, new Rect(left, top, _signWidth, bar));
        Sign(context, brush, new Rect(left, top + bar + gap, _signWidth, _signWidth));
    }

    private static void Sign(DrawingContext context, IBrush brush, Rect box)
    {
        context.DrawRectangle(brush, null, box, 1.5, 1.5);
    }
}

/// <summary>The fill and the contrasting sign of one chart mark.</summary>
internal sealed record ReviewMarkInk(IBrush Fill, IBrush? Sign);
