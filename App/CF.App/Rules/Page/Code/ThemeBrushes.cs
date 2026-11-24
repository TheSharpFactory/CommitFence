using Avalonia.Controls;
using Avalonia.Media;

namespace CF.App.Rules.Page.Code;

/// <summary>
/// The brushes of the colour tokens as the theme in force gives them: what a drawing made in code takes the place of a
/// dynamic resource with.
/// </summary>
internal static class ThemeBrushes
{
    public static IBrush Find(Control from, string token)
    {
        if(from.TryFindResource(token, from.ActualThemeVariant, out var value) && value is IBrush brush)
            return brush;

        return Brushes.Magenta;
    }
}
