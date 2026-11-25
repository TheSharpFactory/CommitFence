using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using CF.Core.Ports;

namespace CF.App.Architecture;

/// <summary>
/// What "Allow this direction" does, in one sentence: the file it writes to and the direction it adds. The two module names are
/// in code type and each stays whole, so a line never breaks inside a name.
/// </summary>
internal sealed class AllowNote : TextBlock
{
    private const string _wordJoiner = "⁠";

    public static readonly StyledProperty<StructureBreach?> BreachProperty =
        AvaloniaProperty.Register<AllowNote, StructureBreach?>(nameof(Breach));

    static AllowNote()
    {
        BreachProperty.Changed.AddClassHandler<AllowNote>((note, _) => note.Rebuild());
    }

    public AllowNote()
    {
        TextWrapping = TextWrapping.Wrap;
        this.Bind(ThemeProperty, this.GetResourceObservable("type-meta"));
        this.Bind(ForegroundProperty, this.GetResourceObservable("ink-2"));
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
        IsVisible = Breach != null;
        if(Breach is not { } breach)
            return;

        this.TryFindResource("code", out var resource);
        var code = resource as ControlTheme;
        Inlines.Add(new Run("Allowing it adds "));
        Inlines.Add(ModuleName(breach.FromModule, code));
        Inlines.Add(new Run(" may use "));
        Inlines.Add(ModuleName(breach.ToModule, code));
        Inlines.Add(new Run(" to commitfence.json."));
    }

    private static Run ModuleName(string module, ControlTheme? theme)
    {
        var run = BreachText.Code(module.Replace("-", "-" + _wordJoiner, StringComparison.Ordinal), theme);
        run.Bind(TextElement.ForegroundProperty, run.GetResourceObservable("ink"));
        return run;
    }
}
