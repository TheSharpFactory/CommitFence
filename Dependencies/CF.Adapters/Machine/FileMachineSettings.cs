using System.Text.Json;
using CF.Core.Ports;

namespace CF.Adapters.Machine;

/// <summary>
/// The app's own switches as a small JSON file in the data folder.
/// </summary>
public sealed class FileMachineSettings : IMachineSettings
{
    private const string _fileName = "machine.json";

    private readonly IDataFolder _dataFolder;

    /// <summary>
    /// Creates the settings that live in this data folder.
    /// </summary>
    public FileMachineSettings(IDataFolder dataFolder)
    {
        _dataFolder = dataFolder;
    }

    /// <inheritdoc />
    public string DataFolder
    {
        get { return _dataFolder.RootFolder; }
    }

    /// <inheritdoc />
    public bool ReduceMotion
    {
        get { return Read(); }
    }

    /// <inheritdoc />
    public void SaveReduceMotion(bool reduce)
    {
        try
        {
            Directory.CreateDirectory(_dataFolder.RootFolder);
            File.WriteAllText(PathOfFile(), JsonSerializer.Serialize(new MachineSettingsDto { ReduceMotion = reduce }, MachineSettingsJsonContext.Default.MachineSettingsDto));
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            // the switch holds until the app ends
        }
    }

    private bool Read()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(PathOfFile()), MachineSettingsJsonContext.Default.MachineSettingsDto)?.ReduceMotion ?? false;
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
        {
            return false;
        }
    }

    private string PathOfFile()
    {
        return Path.Combine(_dataFolder.RootFolder, _fileName);
    }
}
