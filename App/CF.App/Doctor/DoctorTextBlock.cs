using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using CF.App.Parts;
using CF.App.Rules.Page.Code;
using CF.Core.Setup;

namespace CF.App.Doctor;

/// <summary>
/// A sentence of the body in runs: prose in the text style it is given, and each code run (a path, a command, a name) in the code
/// type of the design, in ink. A path breaks after a separator.
/// </summary>
internal sealed class DoctorTextBlock : TextBlock
{
    public static readonly StyledProperty<IReadOnlyList<DoctorTextPart>?> PartsProperty = AvaloniaProperty.Register<DoctorTextBlock, IReadOnlyList<DoctorTextPart>?>(nameof(Parts));

    public static readonly StyledProperty<string?> FailureDetailProperty = AvaloniaProperty.Register<DoctorTextBlock, string?>(nameof(FailureDetail));

    static DoctorTextBlock()
    {
        PartsProperty.Changed.AddClassHandler<DoctorTextBlock>((text, _) => text.Rebuild());
        FailureDetailProperty.Changed.AddClassHandler<DoctorTextBlock>((text, _) => text.Rebuild());
    }

    public DoctorTextBlock()
    {
        TextWrapping = TextWrapping.Wrap;
        ActualThemeVariantChanged += (_, _) => Rebuild();
    }

    public IReadOnlyList<DoctorTextPart>? Parts
    {
        get
        {
            return GetValue(PartsProperty);
        }
        set
        {
            SetValue(PartsProperty, value);
        }
    }

    public string? FailureDetail
    {
        get { return GetValue(FailureDetailProperty); }
        set { SetValue(FailureDetailProperty, value); }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    private void Rebuild()
    {
        Inlines?.Clear();
        if(Parts == null || Inlines == null)
            return;

        var code = CodeStyle();
        foreach(var part in Parts)
        {
            if(!part.IsCode)
            {
                Inlines.Add(new Run(part.Text));
                continue;
            }

            var run = new Run(PathBreaks.After(part.Text));
            if(!Classes.Contains("error"))
                run.Foreground = ThemeBrushes.Find(this, "ink");

            if(code.Family != null)
                run.FontFamily = code.Family;

            if(code.Size != null)
                run.FontSize = code.Size.Value;

            Inlines.Add(run);
        }

        if(!string.IsNullOrEmpty(FailureDetail))
            Inlines.Add(new Run(" " + FailureDetail) { Foreground = ThemeBrushes.Find(this, "ink-2") });
    }

    /// <summary>
    /// The family and the size of the design's <c>code</c> style, read from where the style is defined.
    /// </summary>
    private (FontFamily? Family, double? Size) CodeStyle()
    {
        if(!this.TryFindResource("code", out var found) || found is not ControlTheme theme)
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
