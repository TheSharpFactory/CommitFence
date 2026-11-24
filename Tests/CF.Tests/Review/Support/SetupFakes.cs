using CF.Core.Ports;

namespace CF.Tests.Review.Support;

/// <summary>
/// The user's git settings in memory; every write is recorded.
/// </summary>
internal sealed class RecordingGitSettings : IUserGitSettings
{
    private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

    public List<(string Key, string Value)> Writes { get; } = new List<(string Key, string Value)>();

    public string? Read(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    public void Write(string key, string value)
    {
        Writes.Add((key, value));
        _values[key] = value;
    }
}

/// <summary>
/// A user PATH that can be changed, in memory; every folder added is recorded.
/// </summary>
internal sealed class RecordingUserPath : IUserPath
{
    private readonly List<string> _entries = new List<string>();

    public bool CanChange
    {
        get { return true; }
    }

    public List<string> Added { get; } = new List<string>();

    public bool Contains(string folder)
    {
        return _entries.Contains(folder, StringComparer.Ordinal);
    }

    public void Add(string folder)
    {
        Added.Add(folder);
        _entries.Add(folder);
    }
}
