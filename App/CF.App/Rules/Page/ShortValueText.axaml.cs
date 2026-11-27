using Avalonia;
using Avalonia.Controls;

namespace CF.App.Rules.Page;

/// <summary>
/// The value of a comparison or of a path as one line in the room the token has: the whole value when it fits, else a list's first value
/// with "+N", and a name or a path that is still too long cut in the middle. It tells when it is not showing the whole value, so that the
/// token can offer the whole one in a tooltip.
/// </summary>
internal partial class ShortValueText : UserControl
{
    private const double _moreGap = 8;
    private const double _morePadding = 14;

    public static readonly StyledProperty<IReadOnlyList<string>?> ValuesProperty =
        AvaloniaProperty.Register<ShortValueText, IReadOnlyList<string>?>(nameof(Values));

    public static readonly StyledProperty<bool> IsListProperty = AvaloniaProperty.Register<ShortValueText, bool>(nameof(IsList));

    public static readonly StyledProperty<bool> IsCodeProperty = AvaloniaProperty.Register<ShortValueText, bool>(nameof(IsCode));

    static ShortValueText()
    {
        AffectsMeasure<ShortValueText>(ValuesProperty, IsListProperty, IsCodeProperty);
        IsCodeProperty.Changed.AddClassHandler<ShortValueText>((view, _) => view.Classes.Set("code", view.IsCode));
    }

    public ShortValueText()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The values of the condition, or the one pattern of a path.
    /// </summary>
    public IReadOnlyList<string>? Values
    {
        get { return GetValue(ValuesProperty); }
        set { SetValue(ValuesProperty, value); }
    }

    /// <summary>
    /// Whether the values are a list, which shortens to its first value and "+N".
    /// </summary>
    public bool IsList
    {
        get { return GetValue(IsListProperty); }
        set { SetValue(IsListProperty, value); }
    }

    /// <summary>
    /// Whether the value is typed code: it is drawn in the code face.
    /// </summary>
    public bool IsCode
    {
        get { return GetValue(IsCodeProperty); }
        set { SetValue(IsCodeProperty, value); }
    }

    /// <summary>
    /// Whether what is drawn is less than the whole value.
    /// </summary>
    public bool IsShortened { get; private set; }

    private string _announced = string.Empty;

    /// <summary>
    /// The whole value, as the tooltip says it.
    /// </summary>
    public string WholeText
    {
        get { return ValueShortening.Whole(Values ?? [], IsList); }
    }

    /// <summary>
    /// Raised when what the tooltip says changes: the value starts or stops being shortened, or a shortened value is replaced by another.
    /// </summary>
    public event Action? ShortenedChanged;

    protected override Size MeasureOverride(Size availableSize)
    {
        var values = Values ?? [];
        if(values.Count == 0)
        {
            Text.Text = string.Empty;
            More.IsVisible = false;
            Announce(string.Empty);
            return base.MeasureOverride(availableSize);
        }

        var shown = ValueShortening.Shorten(values, IsList, availableSize.Width, WidthOf, MoreWidth);
        Text.Text = shown.Text;
        MoreText.Text = "+" + shown.More;
        More.IsVisible = shown.More > 0;
        Announce(shown.Text != WholeText ? WholeText : string.Empty);
        return base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// Tells the token when the whole value it offers changed: the text of what is not fully drawn, or nothing when all of it is.
    /// </summary>
    private void Announce(string whole)
    {
        IsShortened = whole.Length > 0;
        if(string.Equals(whole, _announced, StringComparison.Ordinal))
            return;

        _announced = whole;
        ShortenedChanged?.Invoke();
    }

    private double WidthOf(string text)
    {
        Text.Text = text;
        Text.Measure(Size.Infinity);
        return Text.DesiredSize.Width;
    }

    private double MoreWidth(int count)
    {
        MoreText.Text = "+" + count;
        MoreText.Measure(Size.Infinity);
        return _moreGap + _morePadding + MoreText.DesiredSize.Width;
    }
}
