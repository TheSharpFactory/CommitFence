namespace CF.Core.Rules.Settings;

/// <summary>
/// One object or array the scanner is inside of.
/// </summary>
internal sealed class ScanFrame
{
    public ScanFrame(string path, bool isArray)
    {
        Path = path;
        IsArray = isArray;
        CurrentPropertyPath = string.Empty;
    }

    public string Path { get; }

    public bool IsArray { get; }

    public int NextIndex { get; set; }

    public string CurrentPropertyPath { get; set; }

    public HashSet<string> Keys { get; } = new HashSet<string>();
}