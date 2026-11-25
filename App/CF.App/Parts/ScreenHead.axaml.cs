using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;

namespace CF.App.Parts;

/// <summary>
/// The head of a screen: its name, a line of what it shows, and its actions at the right end. The frame tells it how wide the
/// sheet is, and so how much room it keeps at its sides.
/// </summary>
internal sealed partial class ScreenHead : UserControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<ScreenHead, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> MetaProperty = AvaloniaProperty.Register<ScreenHead, string?>(nameof(Meta));

    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<ScreenHead, object?>(nameof(Actions));

    public static readonly StyledProperty<bool> IsOnePaneProperty = AvaloniaProperty.Register<ScreenHead, bool>(nameof(IsOnePane));

    static ScreenHead()
    {
        TitleProperty.Changed.AddClassHandler<ScreenHead>((head, _) => head.ShowWords());
        MetaProperty.Changed.AddClassHandler<ScreenHead>((head, _) => head.ShowWords());
        ActionsProperty.Changed.AddClassHandler<ScreenHead>((head, _) => head.ActionsHost.Content = head.Actions);
        IsOnePaneProperty.Changed.AddClassHandler<ScreenHead>((head, _) => head.Fit());
    }

    public ScreenHead()
    {
        InitializeComponent();
        ShowWords();
    }

    /// <summary>
    /// The screen's name: the one line of the head that is always there.
    /// </summary>
    public string Title
    {
        get
        {
            return GetValue(TitleProperty);
        }
        set
        {
            SetValue(TitleProperty, value);
        }
    }

    /// <summary>
    /// What the screen shows, in one line; none when the screen has nothing to say yet.
    /// </summary>
    public string? Meta
    {
        get
        {
            return GetValue(MetaProperty);
        }
        set
        {
            SetValue(MetaProperty, value);
        }
    }

    /// <summary>
    /// The screen's actions: buttons, 8 apart.
    /// </summary>
    public object? Actions
    {
        get
        {
            return GetValue(ActionsProperty);
        }
        set
        {
            SetValue(ActionsProperty, value);
        }
    }

    public bool IsOnePane
    {
        get
        {
            return GetValue(IsOnePaneProperty);
        }
        set
        {
            SetValue(IsOnePaneProperty, value);
        }
    }

    /// <summary>
    /// The presenter that holds the actions: the keyboard's way into the head.
    /// </summary>
    public ContentPresenter ActionsHost
    {
        get
        {
            return ActionsPlace;
        }
    }

    private void ShowWords()
    {
        NameText.Text = Title;
        MetaText.Text = Meta;
        MetaText.IsVisible = !string.IsNullOrEmpty(Meta);
    }

    private void Fit()
    {
        var side = PageLayout.Side(!IsOnePane);
        Hairline.Padding = new Thickness(side, 4, side, 12);
        Row.MaxWidth = PageLayout.FrameMost(!IsOnePane);
    }
}
