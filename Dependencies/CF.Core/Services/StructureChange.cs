namespace CF.Core.Services;

/// <summary>A saved structure edit, with the information needed to undo it.</summary>
public sealed class StructureChange
{
    internal StructureChange(string path, string before, string after, string message)
    {
        Path = path;
        Before = before;
        After = after;
        Message = message;
    }

    /// <summary>What was saved.</summary>
    public string Message { get; }

    internal string Path { get; }

    internal string Before { get; }

    internal string After { get; }
}
