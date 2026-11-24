using CF.Core.Ports;

namespace CF.Core.Rules.Settings;

/// <summary>
/// Writes changed settings into the settings file safely.
/// </summary>
/// <remarks>
/// The file must still hold the text the change was made from, or nothing is written. The new text must read back
/// through the strict reader, and with the same structure rules, before the file is touched; it is then written beside
/// the file and put in place in one step, so a crash leaves the old file or the new one, never half.
/// </remarks>
internal sealed class SettingsSaver
{
    private const string _temporarySuffix = ".new";

    private readonly IFileSystem _fileSystem;
    private readonly SettingsReader _reader;

    public SettingsSaver(IFileSystem fileSystem, SettingsReader reader)
    {
        _fileSystem = fileSystem;
        _reader = reader;
    }

    /// <summary>
    /// Judges settings as a save would, before anything is written: the text they would be written as must read back the
    /// same. Throws the same <see cref="RuleChangeException" /> a save would.
    /// </summary>
    public void CheckWouldSave(RuleSettings settings, string newline)
    {
        CheckReadsBack(new SettingsWriter(newline).Write(settings), settings);
    }

    public void Save(string settingsPath, string textRead, RuleSettings settings, string newline)
    {
        var text = new SettingsWriter(newline).Write(settings);
        CheckReadsBack(text, settings);

        var temporaryPath = $"{settingsPath}.{Guid.NewGuid():N}{_temporarySuffix}";
        var created = false;
        try
        {
            _fileSystem.WriteNewFile(temporaryPath, text);
            created = true;
            CheckReadsBack(_fileSystem.ReadAllText(temporaryPath), settings);
            CheckUnchanged(settingsPath, textRead);
            _fileSystem.ReplaceFile(temporaryPath, settingsPath);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            RemoveOwnTemporaryFile(temporaryPath, created);
            throw new RuleChangeException("The settings file could not be written, and it is unchanged: " + exception.Message);
        }
        catch(RuleChangeException)
        {
            RemoveOwnTemporaryFile(temporaryPath, created);
            throw;
        }
    }

    private void CheckUnchanged(string settingsPath, string textRead)
    {
        // another command or a person saved the file after it was read: this edit was made on old text, so writing it
        // would lose theirs
        if(!_fileSystem.FileExists(settingsPath) || _fileSystem.ReadAllText(settingsPath) != textRead)
            throw new RuleChangeException($"{settingsPath} changed while this command was working. Nothing was written; run the command again.");
    }

    private void RemoveOwnTemporaryFile(string temporaryPath, bool created)
    {
        if(created)
            _fileSystem.DeleteFile(temporaryPath);
    }

    private void CheckReadsBack(string text, RuleSettings expected)
    {
        RuleSettings readBack;
        try
        {
            readBack = _reader.Read(text);
        }
        catch(SettingsException exception)
        {
            throw new RuleChangeException(exception.Message);
        }

        if(readBack.Structure.Describe() != expected.Structure.Describe() || readBack.Rules.Count != expected.Rules.Count)
            throw new RuleChangeException("The settings would not read back the same, so nothing was written.");
    }
}