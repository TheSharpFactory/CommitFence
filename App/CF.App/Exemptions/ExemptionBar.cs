using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CF.App.Exemptions;

/// <summary>
/// The bar of a rule in the panel "By rule": a neutral bar from the left, as wide as the rule's share of the room, at least 8, with
/// the exemptions that give no reason at its right end in red (its right corners round, the joint square). Centred in the height it is
/// given, no track.
/// </summary>
internal sealed class ExemptionBar : Control
{
    private const double _least = 8;
    private const double _leastRed = 10;
    private const double _leastNeutral = 4;

    public static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<ExemptionBar, double>(nameof(Fraction));

    public static readonly StyledProperty<double> RedFractionProperty = AvaloniaProperty.Register<ExemptionBar, double>(nameof(RedFraction));

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<ExemptionBar, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> RedFillProperty = AvaloniaProperty.Register<ExemptionBar, IBrush?>(nameof(RedFill));

    public static readonly StyledProperty<double> ThicknessProperty = AvaloniaProperty.Register<ExemptionBar, double>(nameof(Thickness), 8);

    static ExemptionBar()
    {
        AffectsRender<ExemptionBar>(FractionProperty, RedFractionProperty, FillProperty, RedFillProperty, ThicknessProperty);
    }

    /// <summary>
    /// The whole bar over the room, 0 to 1.
    /// </summary>
    public double Fraction
    {
        get
        {
            return GetValue(FractionProperty);
        }
        set
        {
            SetValue(FractionProperty, value);
        }
    }

    /// <summary>
    /// The red part over the room, 0 to 1.
    /// </summary>
    public double RedFraction
    {
        get
        {
            return GetValue(RedFractionProperty);
        }
        set
        {
            SetValue(RedFractionProperty, value);
        }
    }

    /// <summary>
    /// The part with a reason.
    /// </summary>
    public IBrush? Fill
    {
        get
        {
            return GetValue(FillProperty);
        }
        set
        {
            SetValue(FillProperty, value);
        }
    }

    /// <summary>
    /// The part with none.
    /// </summary>
    public IBrush? RedFill
    {
        get
        {
            return GetValue(RedFillProperty);
        }
        set
        {
            SetValue(RedFillProperty, value);
        }
    }

    /// <summary>
    /// How high the bar is: 8, or 4 for a rule that steps back.
    /// </summary>
    public double Thickness
    {
        get
        {
            return GetValue(ThicknessProperty);
        }
        set
        {
            SetValue(ThicknessProperty, value);
        }
    }

    public override void Render(DrawingContext context)
    {
        var (neutral, red) = SegmentWidths(Bounds.Width);
        var top = (Bounds.Height - Thickness) / 2;
        var radius = Thickness / 2;
        if(neutral > 0)
        {
            var corners = red > 0 ? new CornerRadius(radius, 0, 0, radius) : new CornerRadius(radius);
            context.DrawRectangle(Fill, null, new RoundedRect(new Rect(0, top, neutral, Thickness), corners));
        }

        if(red > 0)
        {
            var corners = neutral > 0 ? new CornerRadius(0, radius, radius, 0) : new CornerRadius(radius);
            context.DrawRectangle(RedFill, null, new RoundedRect(new Rect(neutral, top, red, Thickness), corners));
        }
    }

    public (double Neutral, double Red) SegmentWidths(double available)
    {
        var whole = Math.Min(available, Math.Max(_least, Math.Clamp(Fraction, 0, 1) * available));
        if(RedFraction <= 0)
            return (whole, 0);

        if(RedFraction >= Fraction)
            return (0, whole);

        // If the whole bar is shorter than both minima combined, keep the joint within its original length.
        var red = whole < _leastRed + _leastNeutral
            ? whole * (_leastRed / (_leastRed + _leastNeutral))
            : Math.Clamp(RedFraction * available, _leastRed, whole - _leastNeutral);
        return (whole - red, red);
    }
}
