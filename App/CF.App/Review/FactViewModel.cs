namespace CF.App.Review;

/// <summary>
/// One fact of the verdict: which icon it has (file, time or branch) and its words.
/// </summary>
internal sealed class FactViewModel
{
    public FactViewModel(string iconKey, string text, string code = "")
    {
        IconKey = iconKey;
        Text = text;
        Code = code;
    }

    public string IconKey { get; }

    public bool IsFiles
    {
        get
        {
            return IconKey == "file";
        }
    }

    public bool IsStaged
    {
        get
        {
            return IconKey == "staged";
        }
    }

    public bool IsCommit
    {
        get
        {
            return IconKey == "commit";
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

    /// <summary>
    /// The words that stand in the fact in the code face, after its text: the short id of a commit.
    /// </summary>
    public string Code { get; }

    public string Text { get; }

    /// <summary>
    /// The fact as one sentence, for the spoken name.
    /// </summary>
    public string Spoken
    {
        get { return Text + Code; }
    }
}
