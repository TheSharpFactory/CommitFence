using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace CF.App.Exemptions;

/// <summary>
/// The panel "By rule". Its rules are one tab stop; Up and Down move between them.
/// </summary>
internal sealed partial class ExemptionRulesPanel : UserControl
{
    public ExemptionRulesPanel()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var step = e.Key == Key.Down ? 1 : e.Key == Key.Up ? -1 : 0;
        if(step == 0 || e.Handled)
            return;

        var stops = this.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("exrule") && button.IsEffectivelyVisible).ToList();
        var at = stops.FindIndex(button => button.IsFocused);
        if(at < 0)
            return;

        stops[Math.Clamp(at + step, 0, stops.Count - 1)].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}
