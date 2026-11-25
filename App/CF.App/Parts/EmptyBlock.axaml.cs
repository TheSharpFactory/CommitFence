using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CF.App.Parts;

/// <summary>
/// The block of a screen with nothing to show yet: the logo, a heading, a lead, anything the screen puts between, and a main
/// button, which is the screen's main button when its head has none.
/// </summary>
internal sealed partial class EmptyBlock : UserControl
{
    public static readonly StyledProperty<string> HeadingProperty = AvaloniaProperty.Register<EmptyBlock, string>(nameof(Heading), string.Empty);

    public static readonly StyledProperty<string?> LeadProperty = AvaloniaProperty.Register<EmptyBlock, string?>(nameof(Lead));

    public static readonly StyledProperty<bool> ShowsEmblemProperty = AvaloniaProperty.Register<EmptyBlock, bool>(nameof(ShowsEmblem), true);

    public static readonly StyledProperty<object?> EmblemProperty = AvaloniaProperty.Register<EmptyBlock, object?>(nameof(Emblem));

    public static readonly StyledProperty<object?> ExtraProperty = AvaloniaProperty.Register<EmptyBlock, object?>(nameof(Extra));

    public static readonly StyledProperty<string?> ButtonTextProperty = AvaloniaProperty.Register<EmptyBlock, string?>(nameof(ButtonText));

    public static readonly StyledProperty<Geometry?> ButtonIconProperty = AvaloniaProperty.Register<EmptyBlock, Geometry?>(nameof(ButtonIcon));

    public static readonly StyledProperty<ICommand?> ButtonCommandProperty = AvaloniaProperty.Register<EmptyBlock, ICommand?>(nameof(ButtonCommand));

    static EmptyBlock()
    {
        HeadingProperty.Changed.AddClassHandler<EmptyBlock>((block, _) => block.Show());
        LeadProperty.Changed.AddClassHandler<EmptyBlock>((block, _) => block.Show());
        ShowsEmblemProperty.Changed.AddClassHandler<EmptyBlock>((block, _) => block.Show());
        EmblemProperty.Changed.AddClassHandler<EmptyBlock>((block, _) => block.Show());
        ExtraProperty.Changed.AddClassHandler<EmptyBlock>((block, _) => block.ExtraHost.Content = block.Extra);
        ButtonTextProperty.Changed.AddClassHandler<EmptyBlock>((block, _) => block.Show());
        ButtonIconProperty.Changed.AddClassHandler<EmptyBlock>((block, _) => block.Show());
        ButtonCommandProperty.Changed.AddClassHandler<EmptyBlock>((block, _) => block.MainButton.Command = block.ButtonCommand);
    }

    public EmptyBlock()
    {
        InitializeComponent();
        Room.SizeChanged += (_, _) => Place();
        Body.SizeChanged += (_, _) => Place();
        Show();
    }

    public string Heading
    {
        get
        {
            return GetValue(HeadingProperty);
        }
        set
        {
            SetValue(HeadingProperty, value);
        }
    }

    public string? Lead
    {
        get
        {
            return GetValue(LeadProperty);
        }
        set
        {
            SetValue(LeadProperty, value);
        }
    }

    /// <summary>
    /// Whether the logo stands above the heading: it does, except where the screen says otherwise.
    /// </summary>
    public bool ShowsEmblem
    {
        get
        {
            return GetValue(ShowsEmblemProperty);
        }
        set
        {
            SetValue(ShowsEmblemProperty, value);
        }
    }

    /// <summary>The screen's specimen in place of the logo.</summary>
    public object? Emblem
    {
        get
        {
            return GetValue(EmblemProperty);
        }
        set
        {
            SetValue(EmblemProperty, value);
        }
    }

    /// <summary>
    /// What stands between the lead and the button: a line of facts, a box of lines.
    /// </summary>
    public object? Extra
    {
        get
        {
            return GetValue(ExtraProperty);
        }
        set
        {
            SetValue(ExtraProperty, value);
        }
    }

    /// <summary>
    /// The words of the main button; none when the block has no button.
    /// </summary>
    public string? ButtonText
    {
        get
        {
            return GetValue(ButtonTextProperty);
        }
        set
        {
            SetValue(ButtonTextProperty, value);
        }
    }

    public Geometry? ButtonIcon
    {
        get
        {
            return GetValue(ButtonIconProperty);
        }
        set
        {
            SetValue(ButtonIconProperty, value);
        }
    }

    public ICommand? ButtonCommand
    {
        get
        {
            return GetValue(ButtonCommandProperty);
        }
        set
        {
            SetValue(ButtonCommandProperty, value);
        }
    }

    /// <summary>
    /// The button of the block, the one stop of the keyboard in it.
    /// </summary>
    public Button Button
    {
        get
        {
            return MainButton;
        }
    }

    private void Show()
    {
        HeadingText.Text = Heading;
        LeadText.Text = Lead;
        LeadText.IsVisible = !string.IsNullOrEmpty(Lead);
        EmblemHost.Content = Emblem;
        EmblemHost.IsVisible = ShowsEmblem && Emblem != null;
        EmblemBox.IsVisible = ShowsEmblem && Emblem == null;
        ButtonLabel.Text = ButtonText;
        ButtonIconMark.Data = ButtonIcon;
        ButtonIconMark.IsVisible = ButtonIcon != null;
        MainButton.IsVisible = !string.IsNullOrEmpty(ButtonText);
        MainButton.SetValue(Avalonia.Automation.AutomationProperties.NameProperty, ButtonText);
    }

    /// <summary>
    /// Puts the block's middle at 40 percent of the pane's height and its top at least 32 below the pane's top.
    /// </summary>
    private void Place()
    {
        if(Room.Bounds.Height <= 0 || Body.Bounds.Height <= 0)
            return;

        var top = PageLayout.EmptyBlockTop(Room.Bounds.Height, Body.Bounds.Height);
        if(Math.Abs(Body.Margin.Top - top) > 0.5)
            Body.Margin = new Thickness(32, top, 32, 0);
    }
}
