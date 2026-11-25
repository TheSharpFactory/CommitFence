using Avalonia;
using Avalonia.Controls;
using CF.App.Rules.Page.Code;

namespace CF.App.Parts;

internal sealed partial class TurningRing : UserControl
{
    public static readonly StyledProperty<bool> OnSurfaceProperty = AvaloniaProperty.Register<TurningRing, bool>(nameof(OnSurface));

    static TurningRing()
    {
        OnSurfaceProperty.Changed.AddClassHandler<TurningRing>((ring, _) => ring.Colour());
    }

    public TurningRing()
    {
        InitializeComponent();
        ActualThemeVariantChanged += (_, _) => Colour();
    }

    /// <summary>
    /// On a surface of hover grey (the update notice) the ring's track is the data colour and its arc ink, 2.5 across.
    /// </summary>
    public bool OnSurface
    {
        get
        {
            return GetValue(OnSurfaceProperty);
        }
        set
        {
            SetValue(OnSurfaceProperty, value);
        }
    }

    /// <summary>
    /// With reduced motion the ring stands still: it still says that something runs, by being there.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Turner.Classes.Set("turn", !Motion.IsReduced);
        Colour();
    }

    private void Colour()
    {
        if(!OnSurface)
            return;

        Track.Stroke = ThemeBrushes.Find(this, "data");
        Arc.Stroke = ThemeBrushes.Find(this, "ink");
        Track.StrokeThickness = 1.75;
        Arc.StrokeThickness = 1.75;
    }
}
