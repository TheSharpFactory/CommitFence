using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace CF.App.Review;

/// <summary>
/// The button that copies a prompt: its look is in the markup, its state in <see cref="CopyButtonViewModel" />.
/// </summary>
internal sealed partial class CopyButton : UserControl
{
    /// <summary>
    /// Opacity 0 while "Copied" shows, 1 at rest: the label at rest is hidden, never removed, so that it keeps the button's width.
    /// </summary>
    public static readonly IValueConverter Hidden = new FuncValueConverter<bool, double>(isCopied => isCopied ? 0 : 1);

    public static readonly StyledProperty<bool> IsCompactProperty = AvaloniaProperty.Register<CopyButton, bool>(nameof(IsCompact));

    public bool IsCompact
    {
        get { return GetValue(IsCompactProperty); }
        set { SetValue(IsCompactProperty, value); }
    }

    public CopyButton()
    {
        InitializeComponent();
    }
}