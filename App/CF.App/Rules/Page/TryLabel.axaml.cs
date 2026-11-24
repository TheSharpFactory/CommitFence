using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using CF.App.Parts;

namespace CF.App.Rules.Page;

/// <summary>
/// A label of Try it: what the rule says about an artifact ("Breaks it", "Follows it", "Not covered"), or that the code does not
/// parse. In the head it holds a count; inside the code it stands after an artifact's first line and may give the reason.
/// </summary>
internal sealed partial class TryLabel : UserControl
{
    private const int _appearMilliseconds = 180;

    public static readonly StyledProperty<TryKind> KindProperty = AvaloniaProperty.Register<TryLabel, TryKind>(nameof(Kind));

    public static readonly StyledProperty<string> WordProperty = AvaloniaProperty.Register<TryLabel, string>(nameof(Word), string.Empty);

    public static readonly StyledProperty<string> ReasonProperty = AvaloniaProperty.Register<TryLabel, string>(nameof(Reason), string.Empty);

    public static readonly StyledProperty<bool> InCodeProperty = AvaloniaProperty.Register<TryLabel, bool>(nameof(InCode));

    public static readonly StyledProperty<bool> IsZeroProperty = AvaloniaProperty.Register<TryLabel, bool>(nameof(IsZero));

    private DispatcherTimer? _appearing;

    public TryLabel()
    {
        InitializeComponent();
        PropertyChanged += OnLabelPropertyChanged;
        Refresh();
    }

    public TryKind Kind
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

    /// <summary>
    /// The word: "1 breaks it" in the head, "Breaks the rule" in the code.
    /// </summary>
    public string Word
    {
        get
        {
            return GetValue(WordProperty);
        }
        set
        {
            SetValue(WordProperty, value);
        }
    }

    /// <summary>
    /// What follows the colon in the code, in the lighter weight. Nothing for a label with no reason.
    /// </summary>
    public string Reason
    {
        get
        {
            return GetValue(ReasonProperty);
        }
        set
        {
            SetValue(ReasonProperty, value);
        }
    }

    /// <summary>
    /// Whether the label stands inside the code (20 high) and not in the head (24 high).
    /// </summary>
    public bool InCode
    {
        get
        {
            return GetValue(InCodeProperty);
        }
        set
        {
            SetValue(InCodeProperty, value);
        }
    }

    /// <summary>
    /// Whether the count the label gives is 0: it is then drawn quiet.
    /// </summary>
    public bool IsZero
    {
        get
        {
            return GetValue(IsZeroProperty);
        }
        set
        {
            SetValue(IsZeroProperty, value);
        }
    }

    /// <summary>
    /// The label grows from its left edge, 0 to 1, in 180 ms with the spring.
    /// </summary>
    public void Appear()
    {
        var scale = new ScaleTransform(0, 1);
        RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
        RenderTransform = scale;
        var started = DateTime.UtcNow;
        _appearing?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
        timer.Tick += (_, _) =>
        {
            var progress = (DateTime.UtcNow - started).TotalMilliseconds / _appearMilliseconds;
            scale.ScaleX = CubicBezierEasing.Spring.Ease(Math.Min(1, progress));
            if(progress < 1)
                return;

            timer.Stop();
            RenderTransform = null;
        };
        _appearing = timer;
        timer.Start();
    }

    private void OnLabelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if(change.Property == KindProperty || change.Property == WordProperty || change.Property == ReasonProperty || change.Property == InCodeProperty || change.Property == IsZeroProperty)
            Refresh();
    }

    private void Refresh()
    {
        if(Back == null)
            return;

        Back.Classes.Set("code", InCode);
        Back.Classes.Set("zero", IsZero);
        Back.Classes.Set("breaks", Kind == TryKind.Breaks);
        Back.Classes.Set("follows", Kind == TryKind.Follows);
        Back.Classes.Set("notcovered", Kind == TryKind.NotCovered);
        Back.Classes.Set("warn", Kind == TryKind.Warn);
        BlockMark.IsVisible = Kind == TryKind.Breaks;
        WarnMark.IsVisible = Kind == TryKind.Warn;
        CheckIcon.IsVisible = Kind == TryKind.Follows;
        OffIcon.IsVisible = Kind == TryKind.NotCovered;
        Words.Inlines ??= new InlineCollection();
        Words.Inlines.Clear();
        Words.Inlines.Add(new Run(Word));
        if(Reason.Length > 0)
            Words.Inlines.Add(new Run($": {Reason}") { FontWeight = FontWeight.Normal });
    }
}
