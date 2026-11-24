using System.Globalization;
using CF.Core.Ports;

namespace CF.Adapters.Updates;

/// <summary>
/// The time of the last check for a newer release, as a small file in the data folder. Each program keeps its own, so
/// that a check by the command line does not stand in for the app's.
/// </summary>
public sealed class FileUpdateCheckRecord : IUpdateCheckRecord
{
    private readonly IDataFolder _dataFolder;
    private readonly string _fileName;

    /// <summary>
    /// Creates the record of the program with this name (commitfence or commitfenceapp).
    /// </summary>
    public FileUpdateCheckRecord(IDataFolder dataFolder, string programName)
    {
        _dataFolder = dataFolder;
        _fileName = "update-check-" + programName;
    }

    /// <inheritdoc />
    public DateTime? LastCheckUtc
    {
        get { return Read(); }
    }

    /// <inheritdoc />
    public void Save(DateTime checkedUtc)
    {
        try
        {
            Directory.CreateDirectory(_dataFolder.RootFolder);
            File.WriteAllText(PathOfFile(), checkedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            // the next start asks again
        }
    }

    private DateTime? Read()
    {
        try
        {
            var text = File.ReadAllText(PathOfFile());
            if(DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time))
                return time.ToUniversalTime();

            return null;
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string PathOfFile()
    {
        return Path.Combine(_dataFolder.RootFolder, _fileName);
    }
}