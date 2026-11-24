using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace CF.App.Review;

/// <summary>
/// The panel of the files of a push, or the count by rule of a folder. Its files with findings (or its rules) are one tab stop; Up and Down move between them.
/// </summary>
internal sealed partial class FilesPanel : UserControl
{
    public FilesPanel()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var step = e.Key == Key.Down ? 1 : e.Key == Key.Up ? -1 : 0;
        if(step == 0 || e.Handled)
            return;

        var stops = this.GetVisualDescendants().OfType<Button>().Where(button => (button.Classes.Contains("panelfile") || button.Classes.Contains("rulerow")) && button.IsEffectivelyVisible).ToList();
        var at = stops.FindIndex(button => button.IsFocused);
        if(at < 0)
            return;

        var next = Math.Clamp(at + step, 0, stops.Count - 1);
        stops[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}
