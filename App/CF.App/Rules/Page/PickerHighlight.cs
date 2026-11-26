using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CF.App.Rules.Page;

/// <summary>
/// Keeps the highlighted entry of a picker in view while the keys move it, so that a list that scrolls inside the picker can be walked
/// from the keyboard alone.
/// </summary>
internal static class PickerHighlight
{
    public static void Reveal(Control panel)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var highlighted = panel.GetVisualDescendants().OfType<Button>().FirstOrDefault(entry => entry.Classes.Contains("highlight"));
            highlighted?.BringIntoView();
        }, DispatcherPriority.Loaded);
    }
}
