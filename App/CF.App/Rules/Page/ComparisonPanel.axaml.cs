using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CF.App.Rules.Page;

/// <summary>
/// The panel of a comparison token and its keys: Up and Down move the highlight (in the field of a number they change the
/// number, in a value list they move between the rows), Enter chooses or keeps the field or adds the next row, Delete takes
/// away an empty row, Esc closes with no change.
/// </summary>
internal sealed partial class ComparisonPanel : UserControl
{
    private ComparisonPickerViewModel? _watched;

    public ComparisonPanel()
    {
        InitializeComponent();
        Surface.AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(TakeKeyboard, DispatcherPriority.Loaded);
        DataContextChanged += (_, _) => Watch();
    }

    private ComparisonPickerViewModel? Model
    {
        get
        {
            return DataContext as ComparisonPickerViewModel;
        }
    }

    private void Watch()
    {
        if(_watched != null)
        {
            _watched.ValueFocusRequested -= OnValueFocusRequested;
            _watched.RowFocusRequested -= OnRowFocusRequested;
        }

        _watched = Model;
        if(_watched != null)
        {
            _watched.ValueFocusRequested += OnValueFocusRequested;
            _watched.RowFocusRequested += OnRowFocusRequested;
        }
    }

    private void OnValueFocusRequested()
    {
        Dispatcher.UIThread.Post(TakeKeyboard, DispatcherPriority.Loaded);
    }

    private void OnRowFocusRequested(ValueRowViewModel row)
    {
        Dispatcher.UIThread.Post(() => FocusRow(row), DispatcherPriority.Loaded);
    }

    private void FocusRow(ValueRowViewModel row)
    {
        var box = this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(candidate => candidate.DataContext == row);
        box?.Focus();
        box?.SelectAll();
    }

    private void TakeKeyboard()
    {
        if(Model?.ShowsField == true)
        {
            Field.Focus();
            Field.SelectAll();
            return;
        }

        if(Model?.ShowsRange == true)
        {
            HighBox.Focus();
            HighBox.SelectAll();
            return;
        }

        if(Model?.ShowsList == true && Model.Rows.Count > 0)
            FocusRow(Model.Rows[0]);
        else
            Surface.Focus();
    }

    private void GiveKeyboardBack()
    {
        var target = this.FindLogicalAncestorOfType<Popup>()?.PlacementTarget;
        target?.Focus(NavigationMethod.Directional);
    }

    private ValueRowViewModel? FocusedRow()
    {
        return (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as TextBox)?.DataContext as ValueRowViewModel;
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
            case Key.Delete when FocusedRow() is { } row:
                return Model?.RemoveEmptyRow(row) == true;
            case Key.Escape:
                Model?.Escape();
                GiveKeyboardBack();
                return true;
            default:
                return false;
        }
    }

    private void StepOrMove(int step)
    {
        var row = FocusedRow();
        if(row != null)
            Model?.MoveRow(row, step);
        else if(Model?.ShowsRange == true && (LowBox.IsKeyboardFocusWithin || HighBox.IsKeyboardFocusWithin))
            Model.StepRange(HighBox.IsKeyboardFocusWithin, -step);
        else if(Model?.ShowsField == true && Field.IsKeyboardFocusWithin)
            Model.Step(-step);
        else
            MoveHighlightInView(step);
    }

    private void MoveHighlightInView(int step)
    {
        Model?.MoveHighlight(step);
        PickerHighlight.Reveal(this);
    }

    private void KeepOrChoose()
    {
        if(Model == null)
            return;

        var row = FocusedRow();
        if(row != null)
            Model.EnterInRow(row);
        else if(Model.ShowsRange && LowBox.IsKeyboardFocusWithin)
            HighBox.Focus();
        else if(Model.ShowsRange && HighBox.IsKeyboardFocusWithin)
            Model.CommitRange();
        else if(Model.ShowsField && Field.IsKeyboardFocusWithin)
            Model.CommitField();
        else
            Model.ChooseHighlighted();

        if(!Model.IsOpen)
            GiveKeyboardBack();
    }
}
