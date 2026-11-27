using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CF.Core.Findings;

namespace CF.App.Settings;

/// <summary>Opens a choice of effects and returns focus to the box after committing or cancelling.</summary>
internal sealed partial class EffectPicker : UserControl
{
    public EffectPicker()
    {
        InitializeComponent();
        Picker.CustomPopupPlacementCallback = PlacePicker;
        Picker.Child!.AddHandler(KeyDownEvent, OnPickerKey, RoutingStrategies.Tunnel);
    }

    private static void PlacePicker(CustomPopupPlacement placement)
    {
        const double gap = 6;
        var height = TopLevel.GetTopLevel(placement.Target)?.Bounds.Height ?? double.PositiveInfinity;
        var below = height - placement.AnchorRectangle.Bottom - gap >= placement.PopupSize.Height;
        placement.Anchor = below ? PopupAnchor.BottomRight : PopupAnchor.TopRight;
        placement.Gravity = below ? PopupGravity.BottomLeft : PopupGravity.TopLeft;
        placement.Offset = new Point(0, below ? gap : -gap);
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY
            | PopupPositionerConstraintAdjustment.ResizeX | PopupPositionerConstraintAdjustment.ResizeY;
    }

    private void OpenPicker(object? sender, RoutedEventArgs e)
    {
        if(DataContext is not EffectEditorViewModel editor)
            return;

        PickerOptions.ItemsSource = new List<EffectPickerOption>
        {
            new EffectPickerOption(Severity.Block, "The push is refused.", editor.Cell.Kind),
            new EffectPickerOption(Severity.Warn, "Reported; the push goes on.", editor.Cell.Kind),
            new EffectPickerOption(null, "Not reported.", editor.Cell.Kind)
        };
        Picker.IsOpen = true;
    }

    private void PickerOpened(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if(Picker.IsOpen)
                Buttons().Find(button => button.DataContext is EffectPickerOption { IsCurrent: true })?.Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Loaded);
    }

    private void ChooseEffect(object? sender, RoutedEventArgs e)
    {
        if(sender is not Button { DataContext: EffectPickerOption option } || DataContext is not EffectEditorViewModel editor)
            return;

        ClosePicker();
        editor.Choose(option.Effect);
    }

    private void OnPickerKey(object? sender, KeyEventArgs e)
    {
        if(e.Key == Key.Escape)
        {
            ClosePicker();
            e.Handled = true;
        }
        else if(e.Key is Key.Up or Key.Down)
        {
            var buttons = Buttons();
            var focused = buttons.FindIndex(button => button.IsKeyboardFocusWithin);
            if(buttons.Count > 0)
                buttons[Math.Clamp(focused + (e.Key == Key.Up ? -1 : 1), 0, buttons.Count - 1)].Focus(NavigationMethod.Directional);

            e.Handled = true;
        }
    }

    private List<Button> Buttons()
    {
        return PickerOptions.GetVisualDescendants().OfType<Button>().ToList();
    }

    private void ClosePicker()
    {
        Picker.IsOpen = false;
        Trigger.Focus(NavigationMethod.Tab);
    }
}

internal sealed class EffectPickerOption(Severity? effect, string description, CF.App.Parts.EffectKind selected)
{
    public StrictnessCellViewModel Cell { get; } = new StrictnessCellViewModel(effect, false);

    public Severity? Effect { get; } = effect;

    public string Description { get; } = description;

    public bool IsCurrent { get { return Cell.Kind == selected; } }

    public string SpokenName { get { return $"{Cell.Word}. {Description}" + (IsCurrent ? " Current effect." : string.Empty); } }
}

internal sealed class EffectPickerButton : Button
{
    public static readonly StyledProperty<string> EffectValueProperty =
        AvaloniaProperty.Register<EffectPickerButton, string>(nameof(EffectValue), string.Empty);

    public string EffectValue
    {
        get { return GetValue(EffectValueProperty); }
        set { SetValue(EffectValueProperty, value); }
    }

    protected override Type StyleKeyOverride { get { return typeof(Button); } }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new EffectPickerButtonPeer(this);
    }

    private sealed class EffectPickerButtonPeer(EffectPickerButton owner) : ButtonAutomationPeer(owner), IValueProvider
    {
        public bool IsReadOnly { get { return true; } }
        public string Value { get { return owner.EffectValue; } }

        public void SetValue(string? value)
        {
            throw new InvalidOperationException("Choose an effect from the picker.");
        }
    }
}
