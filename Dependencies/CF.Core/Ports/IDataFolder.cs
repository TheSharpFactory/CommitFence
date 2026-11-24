namespace CF.Core.Ports;

/// <summary>
/// Where the product keeps what is local to one machine. A port so that checks can point it
/// at a scratch folder.
/// </summary>
public interface IDataFolder
{
    /// <summary>
    /// The folder that holds one folder for each repository.
    /// </summary>
    string RootFolder { get; }
}