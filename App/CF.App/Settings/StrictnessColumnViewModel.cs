namespace CF.App.Settings;

/// <summary>
/// The head of a column of the strictness table; the column in force is marked.
/// </summary>
internal sealed class StrictnessColumnViewModel
{
    public StrictnessColumnViewModel(string name, bool isInForce)
    {
        Name = name;
        IsInForce = isInForce;
    }

    public string Name { get; }

    public bool IsInForce { get; }
}
