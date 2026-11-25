namespace CF.App.Settings;

/// <summary>
/// A heading of the release notes with how many notes stand under it: "The app, 3 notes".
/// </summary>
internal sealed class NoteHeadingViewModel
{
    public NoteHeadingViewModel(string name, int notes)
    {
        Name = name;
        CountText = notes == 0 ? string.Empty : $", {notes} {(notes == 1 ? "note" : "notes")}";
    }

    public string Name { get; }

    /// <summary>
    /// ", 3 notes", or nothing when the heading holds none.
    /// </summary>
    public string CountText { get; }

    public string SpokenName
    {
        get { return Name + CountText; }
    }
}
