using Avalonia;
using Avalonia.Controls;

namespace CF.App.Architecture;

internal static class ArchitectureVisuals
{
    public static TextBlock Text(string text, string theme, string? brush = null)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        block.Bind(StyledElement.ThemeProperty, block.GetResourceObservable(theme));
        if(brush != null)
            block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable(brush));

        return block;
    }

    public static Border Fill(string brush, double width, double height, double radius)
    {
        var border = new Border { Width = width, Height = height, CornerRadius = new CornerRadius(radius) };
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable(brush));
        return border;
    }
}
