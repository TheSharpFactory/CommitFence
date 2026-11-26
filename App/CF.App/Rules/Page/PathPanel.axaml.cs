using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace CF.App.Rules.Page;

/// <summary>
/// The panel of a path token and its keys: Enter keeps the pattern and closes (an empty field removes the path), Esc and Tab
/// close with no change.
/// </summary>
internal sealed partial class PathPanel : UserControl
{
    public PathPanel()
    {
        InitializeComponent();
        Surface.AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(TakeKeyboard, DispatcherPriority.Loaded);
    }

    private PathPieceViewModel? Model
    {
        get
        {
            return DataContext as PathPieceViewModel;
        }
    }

    private void TakeKeyboard()
    {
        Field.Focus();
        Field.CaretIndex = Field.Text?.Length ?? 0;
    }

    private void GiveKeyboardBack()
    {
        var target = this.FindLogicalAncestorOfType<Popup>()?.PlacementTarget;
        target?.Focus(NavigationMethod.Directional);
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if(Model == null)
            return;

        switch(e.Key)
        {
            case Key.Tab:
                Model.Cancel();
                return;
            case Key.Enter:
                if(Model.Commit())
                    GiveKeyboardBack();

                e.Handled = true;
                return;
            case Key.Escape:
                Model.Cancel();
                GiveKeyboardBack();
                e.Handled = true;
                return;
        }
    }
}
