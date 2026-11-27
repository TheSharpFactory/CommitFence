using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using CF.App.Rules.Page;
using CF.App.Rules.Page.Code;

namespace CF.App.Rules;

/// <summary>Fits a rule's other effects on one line, shortening paths in the middle.</summary>
internal sealed class FileEffectText : TextBlock
{
    private const double _pathSize = 12;

    public static readonly StyledProperty<IReadOnlyList<FileEffectScope>> ScopesProperty =
        AvaloniaProperty.Register<FileEffectText, IReadOnlyList<FileEffectScope>>(nameof(Scopes), []);

    static FileEffectText()
    {
        AffectsMeasure<FileEffectText>(ScopesProperty);
    }

    public IReadOnlyList<FileEffectScope> Scopes
    {
        get { return GetValue(ScopesProperty); }
        set { SetValue(ScopesProperty, value); }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Inlines!.Clear();
        var family = this.TryFindResource("code-font", out var resource) && resource is FontFamily code ? code : FontFamily;
        var body = new Typeface(FontFamily);
        var mono = new Typeface(family);
        var labels = Scopes.Select((scope, index) => index == 0 ? scope.Text : ", " + char.ToLowerInvariant(scope.Text[0]) + scope.Text[1..]).ToList();
        var room = Math.Max(0, availableSize.Width - labels.Sum(label => MeasureWidth(label, body, FontSize))
            - Scopes.Sum(scope => MeasureWidth(scope.Suffix, body, FontSize)));
        var remaining = Scopes.Count(scope => scope.Pattern != null);
        for(var index = 0; index < Scopes.Count; index++)
        {
            Inlines.Add(new Run(labels[index]));
            if(Scopes[index].Pattern is not { } path)
                continue;

            var cut = ValueShortening.CutMiddle(path, room / remaining, value => MeasureWidth(value, mono, _pathSize));
            room = Math.Max(0, room - MeasureWidth(cut, mono, _pathSize));
            remaining--;
            Inlines.Add(new Run(cut) { FontFamily = family, FontSize = _pathSize, Foreground = ThemeBrushes.Find(this, "ink") });
            if(Scopes[index].Suffix.Length > 0)
                Inlines.Add(new Run(Scopes[index].Suffix));
        }

        return base.MeasureOverride(availableSize);
    }

    private static double MeasureWidth(string value, Typeface typeface, double size)
    {
        return new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, null).WidthIncludingTrailingWhitespace;
    }
}
