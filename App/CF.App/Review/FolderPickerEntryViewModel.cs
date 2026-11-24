namespace CF.App.Review;

/// <summary>
/// One entry of the folder picker: the whole repository, a folder or project file reviewed before, or one of the two that open
/// the system's own dialog.
/// </summary>
internal sealed class FolderPickerEntryViewModel
{
    public FolderPickerEntryViewModel(FolderPickerEntryKind kind, string label, string note, string path, bool isTicked)
    {
        Kind = kind;
        Label = label;
        Note = note;
        Path = path;
        IsTicked = isTicked;
    }

    public FolderPickerEntryKind Kind { get; }

    /// <summary>
    /// The repository's name, the folder or project file from the repository's root, or the words of the dialog entry.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// "folder" or "project" for a recent one, empty otherwise.
    /// </summary>
    public string Note { get; }

    /// <summary>
    /// The full path of a recent folder or project file; empty for the whole repository and for the dialogs.
    /// </summary>
    public string Path { get; }

    public bool IsTicked { get; }

    public bool IsCode
    {
        get
        {
            return Kind == FolderPickerEntryKind.Recent;
        }
    }

    public bool IsWords
    {
        get
        {
            return Kind != FolderPickerEntryKind.Recent;
        }
    }

    public bool StartsDialogs
    {
        get
        {
            return Kind == FolderPickerEntryKind.ChooseFolder;
        }
    }

    public string SpokenName
    {
        get
        {
            return Note.Length > 0 ? $"{Label}, {Note}" : Label;
        }
    }
}

/// <summary>
/// What a picker entry does.
/// </summary>
internal enum FolderPickerEntryKind
{
    Whole,

    Recent,

    ChooseFolder,

    ChooseProject
}
