namespace CF.Core.Rules.Settings;

/// <summary>
/// Where every key and every array element of the settings text starts, found by its path such as
/// rules[0].where.braces.
/// </summary>
internal sealed class JsonPositions
{
    private readonly LineIndex _lines;
    private readonly Dictionary<string, TextPosition> _positions;
    private readonly List<(string Path, TextPosition Position)> _nulls;

    public JsonPositions(LineIndex lines, Dictionary<string, TextPosition> positions, List<(string Path, TextPosition Position)> nulls)
    {
        _lines = lines;
        _positions = positions;
        _nulls = nulls;
    }

    /// <summary>
    /// The path of a key inside an object.
    /// </summary>
    /// <remarks>
    /// The characters that make up a path (the dot, the brackets and the backslash) are escaped in the key, so that a
    /// key named rules[0] is never taken for the first element of rules.
    /// </remarks>
    public static string Join(string parentPath, string key)
    {
        var escaped = key.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(".", "\\.", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal);
        if(parentPath.Length == 0)
            return escaped;

        return parentPath + "." + escaped;
    }

    /// <summary>
    /// Refuses the first value of the file that is an explicit null, at its place. A key that is meant to have no value
    /// is left out; null is never a way to say so.
    /// </summary>
    public void RejectNulls()
    {
        foreach(var item in _nulls.Where(candidate => candidate.Path.Length > 0))
            throw new SettingsException($"The value at '{item.Path}' is null. Give it a value, or leave it out.", item.Position.Line, item.Position.Column);
    }

    public SettingsException ErrorAt(string path, string message)
    {
        if(_positions.TryGetValue(path, out var position))
            return new SettingsException(message, position.Line, position.Column);

        return new SettingsException(message);
    }

    public SettingsException ErrorAtLine(long zeroBasedLine, long bytePositionInLine, string message)
    {
        var position = _lines.PositionOfLine(zeroBasedLine, bytePositionInLine);
        return new SettingsException(message, position.Line, position.Column);
    }
}