using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace CF.App.Rules.Page;

/// <summary>
/// The panel of a comparison token and its keys: Up and Down move the highlight (in the field of a number they change the
/// number), Enter chooses or keeps the field, Esc closes with no change.
/// </summary>
internal sealed partial class ComparisonPanel : UserControl
{
    public ComparisonPanel()
    {
        InitializeComponent();
        Surface.AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(TakeKeyboard, DispatcherPriority.Loaded);
    }

    private ComparisonPickerViewModel? Model
    {
        get
        {
            return DataContext as ComparisonPickerViewModel;
        }
    }

    private void TakeKeyboard()
    {
        if(Model?.ShowsField == true)
        {
            Field.Focus();
            Field.SelectAll();
        }
        else
        {
            Surface.Focus();
        }
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

        if(!Handle(e.Key))
            return;

        e.Handled = true;
    }

    private bool Handle(Key key)
    {
        switch(key)
        {
            case Key.Up:
                StepOrMove(-1);
                return true;
            case Key.Down:
                StepOrMove(1);
                return true;
            case Key.Enter:
                KeepOrChoose();
                return true;
            case Key.Escape:
                Model?.Close();
                GiveKeyboardBack();
                return true;
            default:
                return false;
        }
    }

    private void StepOrMove(int step)
    {
        if(Model?.ShowsField == true && Field.IsKeyboardFocusWithin)
            Model.Step(-step);
        else
            Model?.MoveHighlight(step);
    }

    private void KeepOrChoose()
    {
        if(Model == null)
            return;

        if(Model.ShowsField && Field.IsKeyboardFocusWithin)
            Model.CommitField();
        else
            Model.ChooseHighlighted();

        if(!Model.IsOpen)
            GiveKeyboardBack();
    }
}
