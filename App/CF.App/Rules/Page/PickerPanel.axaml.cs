using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace CF.App.Rules.Page;

/// <summary>
/// The panel of a picker. Its code is the keys: typing narrows the list, Up, Down, Home and End
/// move the highlight, Enter chooses, Esc closes with no change, and the keyboard goes back to the token.
/// </summary>
internal sealed partial class PickerPanel : UserControl
{
    public PickerPanel()
    {
        InitializeComponent();
        Surface.AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(TakeKeyboard, DispatcherPriority.Loaded);
    }

    private PickerViewModel? Model
    {
        get
        {
            return DataContext as PickerViewModel;
        }
    }

    private void TakeKeyboard()
    {
        if(Model?.HasFindRow == true)
            Find.Focus();
        else
            Surface.Focus();
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

        if(e.Key == Key.Tab)
        {
            Model.Close();
            return;
        }

        if(!Handle(Model, e.Key))
            return;

        e.Handled = true;
    }

    private bool Handle(PickerViewModel model, Key key)
    {
        switch(key)
        {
            case Key.Up:
                model.MoveHighlight(-1);
                PickerHighlight.Reveal(this);
                return true;
            case Key.Down:
                model.MoveHighlight(1);
                PickerHighlight.Reveal(this);
                return true;
            case Key.Home when model.FindText.Length == 0:
                model.MoveHighlight(int.MinValue);
                PickerHighlight.Reveal(this);
                return true;
            case Key.End when model.FindText.Length == 0:
                model.MoveHighlight(int.MaxValue);
                PickerHighlight.Reveal(this);
                return true;
            case Key.Enter:
                model.ChooseHighlighted();
                GiveKeyboardBack();
                return true;
            case Key.Escape:
                model.Escape();
                GiveKeyboardBack();
                return true;
            default:
                return false;
        }
    }
}
