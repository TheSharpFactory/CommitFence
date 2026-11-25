using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using CF.App.Rules.Page.Code;

namespace CF.App.Settings;

/// <summary>
/// A text with two marks in it, as the release notes and the sentence under the strictness table write them: what stands between
/// two ** is bold (weight 600, ink), and what stands between two backticks is code (the strong code type, ink). Everything else is
/// in the style the block is given. A path in code breaks after a separator.
/// </summary>
internal sealed class MarkedText : TextBlock
{
    private const string _boldMark = "**";
    private const char _codeMark = '`';

    public static readonly StyledProperty<string> MarkedProperty = AvaloniaProperty.Register<MarkedText, string>(nameof(Marked), string.Empty);

    static MarkedText()
    {
        MarkedProperty.Changed.AddClassHandler<MarkedText>((text, _) => text.Rebuild());
    }

    public MarkedText()
    {
        TextWrapping = TextWrapping.Wrap;
        ActualThemeVariantChanged += (_, _) => Rebuild();
    }

    public string Marked
    {
        get
        {
            return GetValue(MarkedProperty);
        }
        set
        {
            SetValue(MarkedProperty, value);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    private void Rebuild()
    {
        Inlines?.Clear();
        if(Inlines == null)
            return;

        var code = CodeStyle();
        var ink = ThemeBrushes.Find(this, "ink");
        var bold = false;
        var isCode = false;
        var text = Marked;
        var start = 0;
        for(var at = 0; at < text.Length; at++)
        {
            var boldMark = string.CompareOrdinal(text, at, _boldMark, 0, _boldMark.Length) == 0;
            if(!boldMark && text[at] != _codeMark)
                continue;

            AddRun(text.Substring(start, at - start), bold, isCode, code, ink);
            if(boldMark)
            {
                bold = !bold;
                at++;
            }
            else
            {
                isCode = !isCode;
            }

            start = at + 1;
        }

        AddRun(text.Substring(start), bold, isCode, code, ink);
    }

    private void AddRun(string text, bool bold, bool isCode, (FontFamily? Family, double? Size) code, IBrush ink)
    {
        if(text.Length == 0 || Inlines == null)
            return;

        var run = new Run(isCode ? CF.App.Doctor.PathBreaks.After(text) : text);
        if(bold || isCode)
        {
            run.FontWeight = FontWeight.SemiBold;
            run.Foreground = ink;
        }

        if(isCode)
        {
            if(code.Family != null)
                run.FontFamily = code.Family;

            if(code.Size != null)
                run.FontSize = code.Size.Value;
        }

        Inlines.Add(run);
    }

    /// <summary>
    /// The family and the size of the design's <c>code-strong</c> style, read from where the style is defined.
    /// </summary>
    private (FontFamily? Family, double? Size) CodeStyle()
    {
        if(!this.TryFindResource("code-strong", out var found) || found is not ControlTheme theme)
            return (null, null);

        FontFamily? family = null;
        double? size = null;
        foreach(var setter in theme.Setters.OfType<Setter>())
        {
            if(setter.Property == FontFamilyProperty && setter.Value is FontFamily setFamily)
                family = setFamily;
            else if(setter.Property == FontSizeProperty && setter.Value is double setSize)
                size = setSize;
        }

        return (family, size);
    }
}
