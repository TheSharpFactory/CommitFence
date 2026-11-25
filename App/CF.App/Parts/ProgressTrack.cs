using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace CF.App.Parts;

/// <summary>
/// A progress bar: a track 10 high, radius 5, whose empty part is the data colour, filled from the left as far as the fraction
/// done (the fill slides to each new value over 220 ms). A fraction below 0 says the size is not known: a piece 28 percent of the
/// track wide moves along it. With reduced motion the fill jumps and the piece stands still.
/// </summary>
internal sealed class ProgressTrack : Control
{
    private const double _pieceShare = 0.28;
    private const double _pieceCycleSeconds = 1.4;

    public static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<ProgressTrack, double>(nameof(Fraction));

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<ProgressTrack, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> TrackProperty = AvaloniaProperty.Register<ProgressTrack, IBrush?>(nameof(Track));

    private static readonly StyledProperty<double> _shownProperty = AvaloniaProperty.Register<ProgressTrack, double>("Shown");

    private static readonly StyledProperty<double> _phaseProperty = AvaloniaProperty.Register<ProgressTrack, double>("Phase");

    private CancellationTokenSource? _moving;

    static ProgressTrack()
    {
        AffectsRender<ProgressTrack>(FractionProperty, FillProperty, TrackProperty, _shownProperty, _phaseProperty);
        FractionProperty.Changed.AddClassHandler<ProgressTrack>((track, _) => track.Follow());
    }

    public ProgressTrack()
    {
        Height = 10;
    }

    /// <summary>
    /// How much is done, 0 to 1; below 0 when the size is not known.
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
    /// The empty part.
    /// </summary>
    public IBrush? Track
    {
        get
        {
            return GetValue(TrackProperty);
        }
        set
        {
            SetValue(TrackProperty, value);
        }
    }

    /// <summary>
    /// The part drawn as filled now (it follows the fraction over 220 ms), 0 to 1.
    /// </summary>
    public double ShownFraction
    {
        get
        {
            return GetValue(_shownProperty);
        }
    }

    public override void Render(DrawingContext context)
    {
        var radius = Bounds.Height / 2;
        context.DrawRectangle(Track, null, new RoundedRect(new Rect(Bounds.Size), radius));
        if(Fraction < 0)
        {
            DrawPiece(context, radius);
            return;
        }

        var width = Math.Clamp(GetValue(_shownProperty), 0, 1) * Bounds.Width;
        if(width > 0)
            context.DrawRectangle(Fill, null, new RoundedRect(new Rect(0, 0, Math.Max(width, Bounds.Height), Bounds.Height), radius));
    }

    private void DrawPiece(DrawingContext context, double radius)
    {
        var width = Bounds.Width * _pieceShare;
        var left = Motion.IsReduced ? (Bounds.Width - width) / 2 : (GetValue(_phaseProperty) * (Bounds.Width + width)) - width;
        using(context.PushClip(new RoundedRect(new Rect(Bounds.Size), radius)))
        {
            context.DrawRectangle(Fill, null, new RoundedRect(new Rect(left, 0, width, Bounds.Height), radius));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Follow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopMoving();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// The fraction changed: the fill slides to it (at once with reduced motion), or the piece starts to move.
    /// </summary>
    private void Follow()
    {
        if(Fraction < 0)
        {
            StartMoving();
            return;
        }

        StopMoving();
        Transitions = Motion.IsReduced ? null : SlidingFill();

        SetValue(_shownProperty, Fraction);
    }

    /// <summary>
    /// The fill slides to each new value over 220 ms; the transitions are made once.
    /// </summary>
    private Transitions SlidingFill()
    {
        return Transitions ?? new Transitions { new DoubleTransition { Property = _shownProperty, Duration = TimeSpan.FromMilliseconds(220), Easing = CubicBezierEasing.Standard } };
    }

    private void StartMoving()
    {
        if(_moving != null || Motion.IsReduced || VisualRoot == null)
            return;

        _moving = new CancellationTokenSource();
        _ = new Animation
        {
            Duration = TimeSpan.FromSeconds(_pieceCycleSeconds),
            IterationCount = IterationCount.Infinite,
            Easing = new LinearEasing(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(_phaseProperty, 0.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(_phaseProperty, 1.0) } }
            }
        }.RunAsync(this, _moving.Token);
    }

    private void StopMoving()
    {
        _moving?.Cancel();
        _moving?.Dispose();
        _moving = null;
    }
}
