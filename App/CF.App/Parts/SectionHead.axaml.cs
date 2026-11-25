using Avalonia;
using Avalonia.Controls;

namespace CF.App.Parts;

/// <summary>
/// The head of a section of a screen: its name, a note, and a small button at the right end. Sections stand 48 apart (the screen
/// puts that room between them).
/// </summary>
internal sealed partial class SectionHead : UserControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<SectionHead, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> NoteProperty = AvaloniaProperty.Register<SectionHead, string?>(nameof(Note));

    public static readonly StyledProperty<object?> ActionProperty = AvaloniaProperty.Register<SectionHead, object?>(nameof(Action));

    static SectionHead()
    {
        TitleProperty.Changed.AddClassHandler<SectionHead>((head, _) => head.ShowWords());
        NoteProperty.Changed.AddClassHandler<SectionHead>((head, _) => head.ShowWords());
        ActionProperty.Changed.AddClassHandler<SectionHead>((head, _) => head.ActionPlace.Content = head.Action);
    }

    public SectionHead()
    {
        InitializeComponent();
        ShowWords();
    }

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
    /// A note about the section, in one line; none when the section has none.
    /// </summary>
    public string? Note
    {
        get
        {
            return GetValue(NoteProperty);
        }
        set
        {
            SetValue(NoteProperty, value);
        }
    }

    /// <summary>
    /// The small button at the right end, if the section has one.
    /// </summary>
    public object? Action
    {
        get
        {
            return GetValue(ActionProperty);
        }
        set
        {
            SetValue(ActionProperty, value);
        }
    }

    private void ShowWords()
    {
        NameText.Text = Title;
        NoteText.Text = Note;
        NoteText.IsVisible = !string.IsNullOrEmpty(Note);
    }
}
