using System.Security.Cryptography;
using System.Text;
using CF.Core.Ports;

namespace CF.Core.Rules.Settings;

/// <summary>
/// The settings file as a file: reads its text through the file system port and hands it to the reader, so that every
/// way into the file says the same thing when it is missing or cannot be read.
/// </summary>
public sealed class SettingsFile
{
    private static readonly Encoding[] _markedEncodings =
        [Encoding.UTF32, new UTF32Encoding(true, true), Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode];

    private readonly IFileSystem _fileSystem;
    private readonly SettingsReader _reader;

    /// <summary>
    /// Creates the helper over a file system and a reader.
    /// </summary>
    public SettingsFile(IFileSystem fileSystem, SettingsReader reader)
    {
        _fileSystem = fileSystem;
        _reader = reader;
    }

    /// <summary>
    /// The settings in the file, which must exist. Throws a <see cref="SettingsException" /> when the file is missing,
    /// cannot be read or is wrong.
    /// </summary>
    public RuleSettings Read(string settingsPath)
    {
        return _reader.Read(ReadText(settingsPath));
    }

    /// <summary>Reads settings when a file exists, applying the same validation as a required read.</summary>
    public RuleSettings? ReadIfPresent(string settingsPath)
    {
        return _fileSystem.FileExists(settingsPath) ? Read(settingsPath) : null;
    }

    /// <summary>
    /// The text of the file, which must exist. Throws a <see cref="SettingsException" /> when the file is missing or
    /// cannot be read.
    /// </summary>
    public string ReadText(string settingsPath)
    {
        return ReadFrom(settingsPath, _fileSystem.ReadAllText);
    }

    internal (string Text, string Revision) ReadVersion(string settingsPath)
    {
        return ReadFrom(settingsPath, path =>
        {
            using var file = _fileSystem.OpenRead(path);
            using var snapshot = new MemoryStream();
            file.CopyTo(snapshot);
            snapshot.Position = 0;
            var revision = RevisionOf(snapshot);
            return (Decode(snapshot.GetBuffer().AsSpan(0, (int)snapshot.Length)), revision);
        });
    }

    private static string Decode(ReadOnlySpan<byte> bytes)
    {
        foreach(var encoding in _markedEncodings)
        {
            if(bytes.StartsWith(encoding.Preamble))
                return encoding.GetString(bytes[encoding.Preamble.Length..]);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    internal static string RevisionOf(Stream bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private T ReadFrom<T>(string settingsPath, Func<string, T> read)
    {
        if(!_fileSystem.FileExists(settingsPath))
            throw new SettingsException("The settings file does not exist.");

        try
        {
            return read(settingsPath);
        }
        catch(IOException exception)
        {
            throw new SettingsException("The settings file cannot be read: " + exception.Message);
        }
        catch(UnauthorizedAccessException exception)
        {
            throw new SettingsException("The settings file cannot be read: " + exception.Message);
        }
    }

    /// <summary>
    /// The requires key of the file, read leniently. Nothing when the file is missing or cannot be read, or has no such
    /// key.
    /// </summary>
    public string? ReadRequires(string settingsPath)
    {
        if(!_fileSystem.FileExists(settingsPath))
            return null;

        try
        {
            return SettingsReader.ReadRequires(_fileSystem.ReadAllText(settingsPath));
        }
        catch(IOException)
        {
            return null;
        }
        catch(UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The settings the text holds, as the reader reads them.
    /// </summary>
    public RuleSettings Parse(string text)
    {
        return _reader.Read(text);
    }

    internal string? HigherFeatureVersion(RuleSettings settings)
    {
        return _reader.HigherFeatureVersion(settings);
    }
}
