using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CF.App.Parts;

/// <summary>
/// The focus ring, drawn around the control it is laid over: a band in the colour of the
/// surface directly outside the control's edge, then a line outside the band. Both follow the control's shape. It draws
/// beyond its own bounds, so it does not change the layout, and it takes no clicks.
/// </summary>
internal sealed class FocusRing : Control
{
    public static readonly StyledProperty<double> ControlRadiusProperty = AvaloniaProperty.Register<FocusRing, double>(nameof(ControlRadius));

    public static readonly StyledProperty<double> GapThicknessProperty = AvaloniaProperty.Register<FocusRing, double>(nameof(GapThickness), 2);

    public static readonly StyledProperty<double> LineThicknessProperty = AvaloniaProperty.Register<FocusRing, double>(nameof(LineThickness), 2);

    public static readonly StyledProperty<IBrush?> GapBrushProperty = AvaloniaProperty.Register<FocusRing, IBrush?>(nameof(GapBrush));

    public static readonly StyledProperty<IBrush?> LineBrushProperty = AvaloniaProperty.Register<FocusRing, IBrush?>(nameof(LineBrush));

    static FocusRing()
    {
        AffectsRender<FocusRing>(ControlRadiusProperty, GapThicknessProperty, LineThicknessProperty, GapBrushProperty, LineBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<FocusRing>(false);
    }

    /// <summary>
    /// The corner radius of the control that the ring encloses.
    /// </summary>
    public double ControlRadius
    {
        get
        {
            return GetValue(ControlRadiusProperty);
        }
        set
        {
            SetValue(ControlRadiusProperty, value);
        }
    }

    /// <summary>
    /// The thickness of the band next to the control: 2, and 3 around a switch.
    /// </summary>
    public double GapThickness
    {
        get
        {
            return GetValue(GapThicknessProperty);
        }
        set
        {
            SetValue(GapThicknessProperty, value);
        }
    }

    public double LineThickness
    {
        get
        {
            return GetValue(LineThicknessProperty);
        }
        set
        {
            SetValue(LineThicknessProperty, value);
        }
    }

    public IBrush? GapBrush
    {
        get
        {
            return GetValue(GapBrushProperty);
        }
        set
        {
            SetValue(GapBrushProperty, value);
        }
    }

    public IBrush? LineBrush
    {
        get
        {
            return GetValue(LineBrushProperty);
        }
        set
        {
            SetValue(LineBrushProperty, value);
        }
    }

    public override void Render(DrawingContext context)
    {
        var edge = new Rect(Bounds.Size);
        DrawBand(context, GapBrush, edge, 0, GapThickness);
        DrawBand(context, LineBrush, edge, GapThickness, LineThickness);
    }

    private void DrawBand(DrawingContext context, IBrush? brush, Rect edge, double distance, double thickness)
    {
        if(brush == null)
            return;

        var middle = distance + (thickness / 2);
        var radius = ControlRadius + middle;
        context.DrawRectangle(null, new Pen(brush, thickness), edge.Inflate(middle), radius, radius);
    }
}
