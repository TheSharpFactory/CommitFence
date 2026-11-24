namespace CF.App.Review;

/// <summary>
/// One fact of the verdict: which icon it has (file, time or branch) and its words.
/// </summary>
internal sealed class FactViewModel
{
    public FactViewModel(string iconKey, string text)
    {
        IconKey = iconKey;
        Text = text;
    }

    public string IconKey { get; }

    public bool IsFiles
    {
        get
        {
            return IconKey == "file";
        }
    }

    public bool IsTime
    {
        get
        {
            return IconKey == "time";
        }
    }

    public bool IsCommits
    {
        get
        {
            return IconKey == "branch";
        }
    }

    public string Text { get; }
}
