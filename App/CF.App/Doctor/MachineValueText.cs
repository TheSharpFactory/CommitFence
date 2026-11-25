using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace CF.App.Doctor;

/// <summary>
/// The value of a fact of "This machine": selectable text in the code type that breaks after a separator, and copies as it was
/// written, without the places made for breaking. It is not a stop of the Tab key.
/// </summary>
internal sealed class MachineValueText : SelectableTextBlock
{
    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<MachineValueText, string>(nameof(Value), string.Empty);

    static MachineValueText()
    {
        ValueProperty.Changed.AddClassHandler<MachineValueText>((text, _) => text.Text = PathBreaks.After(text.Value));
    }

    public MachineValueText()
    {
        IsTabStop = false;
        CopyingToClipboard += OnCopying;
    }

    public string Value
    {
        get
        {
            return GetValue(ValueProperty);
        }
        set
        {
            SetValue(ValueProperty, value);
        }
    }

    private async void OnCopying(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if(clipboard == null)
            return;

        e.Handled = true;
        await clipboard.SetTextAsync(PathBreaks.Without(SelectedText));
    }
}
