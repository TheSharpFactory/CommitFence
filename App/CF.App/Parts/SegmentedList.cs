using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CF.App.Parts;

/// <summary>
/// The list behind a segmented control: one tab stop, and Left and Right move the choice (and the ring with it).
/// </summary>
internal sealed class SegmentedList : ListBox
{
    protected override Type StyleKeyOverride
    {
        get
        {
            return typeof(ListBox);
        }
    }

    public SegmentedList()
    {
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var step = e.Key == Key.Right ? 1 : e.Key == Key.Left ? -1 : 0;
        if(step == 0)
            return;

        var next = Math.Clamp(SelectedIndex + step, 0, ItemCount - 1);
        SelectedIndex = next;
        ContainerFromIndex(next)?.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}
