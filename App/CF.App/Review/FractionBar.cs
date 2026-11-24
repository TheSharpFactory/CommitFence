using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CF.App.Review;

/// <summary>
/// The bar of a rule in the count by rule: a rounded bar from the left, as wide as its fraction of the room, at least 8, centred in
/// the height it is given, no track.
/// </summary>
internal sealed class FractionBar : Control
{
    private const double _least = 8;

    public static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<FractionBar, double>(nameof(Fraction));

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<FractionBar, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> ThicknessProperty = AvaloniaProperty.Register<FractionBar, double>(nameof(Thickness), 8);

    static FractionBar()
    {
        AffectsRender<FractionBar>(FractionProperty, FillProperty, ThicknessProperty);
    }

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
        var width = Math.Max(_least, Math.Clamp(Fraction, 0, 1) * Bounds.Width);
        var top = (Bounds.Height - Thickness) / 2;
        context.DrawRectangle(Fill, null, new RoundedRect(new Rect(0, top, Math.Min(width, Bounds.Width), Thickness), Thickness / 2));
    }
}
