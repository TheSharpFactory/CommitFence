namespace CF.Core.Structure;

/// <summary>
/// One direction between two modules, written in the settings file as from -> to: code of the first module uses code of
/// the second.
/// </summary>
public sealed class ModulePair
{
    /// <summary>
    /// Creates a pair.
    /// </summary>
    public ModulePair(string from, string to)
    {
        From = from;
        To = to;
    }

    /// <summary>
    /// The module whose code uses.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// The module whose code is used.
    /// </summary>
    public string To { get; }

    /// <summary>
    /// Whether this pair is the direction from the first module to the second.
    /// </summary>
    public bool Is(string from, string to)
    {
        return From == from && To == to;
    }

    /// <summary>
    /// The pair as it is written in the settings file.
    /// </summary>
    public override string ToString()
    {
        return From + " -> " + To;
    }
}