using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CF.App.Parts;

/// <summary>
/// The mark of an effect: the stop sign, the triangle or the off sign. It is two drawings, the shape and its sign, each
/// with a brush of its own, so that the place the mark stands in can change the sign's colour.
/// </summary>
internal sealed class EffectMark : Control
{
    private const double _box = 16;

    private static readonly Geometry _stopShape = Geometry.Parse("M5.1 1.5h5.8L15 5.6v4.8l-4.1 4.1H5.1L1 10.4V5.6z");
    private static readonly Geometry _stopSign = Geometry.Parse("M4.6 8h6.8");
    private static readonly Geometry _triangleShape = Geometry.Parse("M8 2.2l6.3 11.3H1.7z");
    private static readonly Geometry _triangleSign = Geometry.Parse("M8 6.4v3.4M8 11.9v.1");
    private static readonly Geometry _offShape = Geometry.Parse("M8 2.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11zM5.5 8h5");

    public static readonly StyledProperty<EffectKind> KindProperty = AvaloniaProperty.Register<EffectMark, EffectKind>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> ShapeProperty = AvaloniaProperty.Register<EffectMark, IBrush?>(nameof(Shape));

    public static readonly StyledProperty<IBrush?> SignProperty = AvaloniaProperty.Register<EffectMark, IBrush?>(nameof(Sign));

    static EffectMark()
    {
        AffectsRender<EffectMark>(KindProperty, ShapeProperty, SignProperty);
    }

    public EffectKind Kind
    {
        get
        {
            return GetValue(KindProperty);
        }
        set
        {
            SetValue(KindProperty, value);
        }
    }

    public IBrush? Shape
    {
        get
        {
            return GetValue(ShapeProperty);
        }
        set
        {
            SetValue(ShapeProperty, value);
        }
    }

    public IBrush? Sign
    {
        get
        {
            return GetValue(SignProperty);
        }
        set
        {
            SetValue(SignProperty, value);
        }
    }

    public override void Render(DrawingContext context)
    {
        var scale = Math.Min(Bounds.Width, Bounds.Height) / _box;
        using(context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            switch(Kind)
            {
                case EffectKind.Block:
                    DrawStop(context);
                    break;
                case EffectKind.Warn:
                    DrawTriangle(context);
                    break;
                default:
                    DrawOff(context);
                    break;
            }
        }
    }

    private void DrawStop(DrawingContext context)
    {
        context.DrawGeometry(Shape, null, _stopShape);
        context.DrawGeometry(null, RoundPen(Sign, 2), _stopSign);
    }

    private void DrawTriangle(DrawingContext context)
    {
        context.DrawGeometry(Shape, RoundPen(Shape, 1.6), _triangleShape);
        context.DrawGeometry(null, RoundPen(Sign, 1.6), _triangleSign);
    }

    private void DrawOff(DrawingContext context)
    {
        context.DrawGeometry(null, RoundPen(Shape, 1.5), _offShape);
    }

    private static Pen RoundPen(IBrush? brush, double thickness)
    {
        return new Pen(brush, thickness, null, PenLineCap.Round, PenLineJoin.Round);
    }
}
