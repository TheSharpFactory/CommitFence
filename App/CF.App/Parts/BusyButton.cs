using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace CF.App.Parts;

/// <summary>A working button keeps focus and ignores activation until its work finishes.</summary>
internal sealed class BusyButton : Button
{
    public static readonly StyledProperty<bool> IsBusyProperty = AvaloniaProperty.Register<BusyButton, bool>(nameof(IsBusy));

    static BusyButton()
    {
        WidthProperty.OverrideDefaultValue<BusyButton>(132);
        IsBusyProperty.Changed.AddClassHandler<BusyButton>((button, _) => button.Classes.Set("busy", button.IsBusy));
    }

    public bool IsBusy
    {
        get { return GetValue(IsBusyProperty); }
        set { SetValue(IsBusyProperty, value); }
    }

    protected override Type StyleKeyOverride { get { return typeof(Button); } }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if(IsBusy)
            e.Handled = true;
        else
            base.OnPointerPressed(e);
    }

    protected override void OnClick()
    {
        if(!IsBusy)
            base.OnClick();
    }
}
