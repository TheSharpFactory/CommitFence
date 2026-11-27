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

    public bool ExplicitChoice { get; set; }

    public SegmentedList()
    {
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if(ExplicitChoice && e.Source is ListBoxItem item)
        {
            OnExplicitKey(e, item);
            return;
        }

        var step = e.Key == Key.Right ? 1 : e.Key == Key.Left ? -1 : 0;
        if(step == 0)
            return;

        var next = Math.Clamp(SelectedIndex + step, 0, ItemCount - 1);
        SelectedIndex = next;
        ContainerFromIndex(next)?.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private void OnExplicitKey(KeyEventArgs e, ListBoxItem item)
    {
        var index = IndexFromContainer(item);
        if(e.Key is Key.Enter or Key.Space)
            SelectedIndex = index;
        else if(e.Key is Key.Left or Key.Up)
            FocusChoice(index - 1);
        else if(e.Key is Key.Right or Key.Down)
            FocusChoice(index + 1);
        else if(e.Key == Key.Home)
            FocusChoice(0);
        else if(e.Key == Key.End)
            FocusChoice(ItemCount - 1);
        else
            return;

        e.Handled = true;
    }

    private void FocusChoice(int index)
    {
        ContainerFromIndex(Math.Clamp(index, 0, ItemCount - 1))?.Focus(NavigationMethod.Tab);
    }
}
