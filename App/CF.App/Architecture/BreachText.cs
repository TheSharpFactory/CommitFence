using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using CF.App.Parts;
using CF.Core.Ports;

namespace CF.App.Architecture;

/// <summary>A historical use with its identifiers and source path in code type.</summary>
internal sealed class BreachText : TextBlock
{
    public static readonly StyledProperty<StructureBreach?> BreachProperty =
        AvaloniaProperty.Register<BreachText, StructureBreach?>(nameof(Breach));

    static BreachText()
    {
        BreachProperty.Changed.AddClassHandler<BreachText>((text, _) => text.Rebuild());
    }

    public BreachText()
    {
        TextWrapping = TextWrapping.Wrap;
    }

    public StructureBreach? Breach
    {
        get { return GetValue(BreachProperty); }
        set { SetValue(BreachProperty, value); }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    private void Rebuild()
    {
        if(Inlines == null)
            return;

        Inlines.Clear();
        if(Breach is not { } breach)
            return;

        this.TryFindResource("code-strong", out var resource);
        var code = resource as ControlTheme;
        Inlines.Add(Code(breach.FromModule, code));
        Inlines.Add(new Run(" may not use "));
        Inlines.Add(Code(breach.ToModule, code));
        Inlines.Add(new Run(". "));
        Inlines.Add(breach.Member.Length == 0 ? new Run("Code outside any member") : Code(breach.Member, code));
        Inlines.Add(new Run(" uses "));
        Inlines.Add(Code(breach.UsedType, code));
        Inlines.Add(new Run(", in "));
        Inlines.Add(Code(PathBreaks.After(breach.File), code));
        Inlines.Add(new Run(", line " + breach.Line.ToString(CultureInfo.CurrentCulture) + "."));
    }

    public static Run Code(string text, ControlTheme? theme)
    {
        var run = new Run(text);
        if(theme == null)
            return run;

        foreach(var setter in theme.Setters.OfType<Setter>())
        {
            if(setter.Property == FontFamilyProperty && setter.Value is FontFamily family)
                run.FontFamily = family;
            else if(setter.Property == FontSizeProperty && setter.Value is double size)
                run.FontSize = size;
            else if(setter.Property == FontWeightProperty && setter.Value is FontWeight weight)
                run.FontWeight = weight;
        }

        return run;
    }
}
