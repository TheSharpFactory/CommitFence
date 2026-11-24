using CF.Core.Ports;

namespace CF.Core.Rules.Settings;

/// <summary>
/// The settings file as a file: reads its text through the file system port and hands it to the reader, so that every
/// way into the file says the same thing when it is missing or cannot be read.
/// </summary>
public sealed class SettingsFile
{
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

    /// <summary>
    /// The text of the file, which must exist. Throws a <see cref="SettingsException" /> when the file is missing or
    /// cannot be read.
    /// </summary>
    public string ReadText(string settingsPath)
    {
        if(!_fileSystem.FileExists(settingsPath))
            throw new SettingsException("The settings file does not exist.");

        try
        {
            return _fileSystem.ReadAllText(settingsPath);
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
}