namespace CF.App.Review;

/// <summary>
/// One segment of the choice of what to review: the push, the staged changes (what the next commit would record) or a folder.
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

    public bool IsStaged
    {
        get
        {
            return IconKey == "staged";
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
            if(!IsAvailable)
                return Label + ", not available yet";

            return IsStaged ? "Staged changes, what the next commit would record" : Label;
        }
    }
}
