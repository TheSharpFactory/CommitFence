namespace CF.App.Doctor;

/// <summary>
/// One fact of "This machine": its name and its value, or no value when the checks could not find it.
/// </summary>
internal sealed class MachineRowViewModel
{
    public MachineRowViewModel(string name, string? value, bool hasLine = true)
    {
        Name = name;
        HasLine = hasLine;
        Value = value ?? string.Empty;
        IsKnown = value != null;
    }

    public string Name { get; }

    /// <summary>
    /// A hairline stands under the row: under every row but the last.
    /// </summary>
    public bool HasLine { get; }

    public string Value { get; }

    public bool IsKnown { get; }

    public bool IsUnknown
    {
        get { return !IsKnown; }
    }

    public string SpokenName
    {
        get { return $"{Name}: {(IsKnown ? Value : "unknown")}"; }
    }
}
