namespace CF.App.Review;

/// <summary>
/// One segment of the choice of what to review. "A folder" is drawn and cannot be chosen yet: it is the next piece.
/// </summary>
internal sealed class ReviewChoiceViewModel
{
    public ReviewChoiceViewModel(string label, string iconKey, bool isAvailable)
    {
        Label = label;
        IconKey = iconKey;
        IsAvailable = isAvailable;
    }

    public string Label { get; }

    public string IconKey { get; }

    public bool IsChanges
    {
        get
        {
            return IconKey == "branch";
        }
    }

    public bool IsFolder
    {
        get
        {
            return IconKey == "folder";
        }
    }

    public bool IsAvailable { get; }

    public string SpokenName
    {
        get
        {
            return IsAvailable ? Label : Label + ", not available yet";
        }
    }
}
