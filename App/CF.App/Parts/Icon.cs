using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CF.App.Parts;

/// <summary>
/// One of the app's icons: a path drawn on a 16 by 16 box, stroked in the colour of the text beside it, scaled to the
/// size the control is given.
/// </summary>
internal sealed class Icon : Control
{
    private const double _box = 16;

    public static readonly StyledProperty<Geometry?> DataProperty = AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<Geometry?> FillDataProperty = AvaloniaProperty.Register<Icon, Geometry?>(nameof(FillData));

    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<Icon, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<Icon, double>(nameof(StrokeThickness), 1.5);

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, FillDataProperty, StrokeProperty, StrokeThicknessProperty);
    }

    public Geometry? Data
    {
        get
        {
            return GetValue(DataProperty);
        }
        set
        {
            SetValue(DataProperty, value);
        }
    }

    public Geometry? FillData
    {
        get { return GetValue(FillDataProperty); }
        set { SetValue(FillDataProperty, value); }
    }

    public IBrush? Stroke
    {
        get
        {
            return GetValue(StrokeProperty);
        }
        set
        {
            SetValue(StrokeProperty, value);
        }
    }

    public double StrokeThickness
    {
        get
        {
            return GetValue(StrokeThicknessProperty);
        }
        set
        {
            SetValue(StrokeThicknessProperty, value);
        }
    }

    public override void Render(DrawingContext context)
    {
        if(Data == null || Stroke == null)
            return;

        var scale = Math.Min(Bounds.Width, Bounds.Height) / _box;
        var pen = new Pen(Stroke, StrokeThickness, null, PenLineCap.Round, PenLineJoin.Round);
        using(context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            context.DrawGeometry(null, pen, Data);
            if(FillData != null)
                context.DrawGeometry(Stroke, null, FillData);
        }
    }
}
