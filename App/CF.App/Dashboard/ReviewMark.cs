using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CF.App.Rules.Page.Code;
using CF.Core.Review;

namespace CF.App.Dashboard;

/// <summary>One review mark, shared by the chart and its keys.</summary>
internal sealed class ReviewMark : Control
{
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
        Draw(context, new Rect(Bounds.Size), Kind, InkOf(this, Kind), true, true);
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

    public static void Draw(DrawingContext context, Rect box, ReviewOutcome kind, ReviewMarkInk ink, bool mark, bool key = false)
    {
        var radius = key ? box.Height / 4 : Math.Min(4, box.Height / 2);
        context.DrawRectangle(ink.Fill, null, box, radius, radius);
        if(!mark || ink.Sign == null)
            return;

        var center = box.X + (box.Width / 2);
        if(kind == ReviewOutcome.Blocked)
        {
            var inset = key ? box.Width / 4 : 4;
            Sign(context, ink.Sign, new Rect(box.X + inset, box.Y + ((box.Height - 2.5) / 2), box.Width - (2 * inset), 2.5));
            return;
        }

        var scale = box.Height / 20;
        Sign(context, ink.Sign, new Rect(center - 1.25, box.Y + (4 * scale), 2.5, 7.5 * scale));
        Sign(context, ink.Sign, new Rect(center - 1.25, box.Y + (13.25 * scale), 2.5, 2.5 * scale));
    }

    private static void Sign(DrawingContext context, IBrush brush, Rect box)
    {
        context.DrawRectangle(brush, null, box, 1.25, 1.25);
    }
}

/// <summary>The fill and the contrasting sign of one chart mark.</summary>
internal sealed record ReviewMarkInk(IBrush Fill, IBrush? Sign);
