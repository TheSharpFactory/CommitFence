using CF.Core.Ports;

namespace CF.Adapters.Machine;

/// <summary>
/// The per-user data folder of the machine.
/// </summary>
/// <remarks>
/// It is under the local application data folder, or the folder the environment variable COMMITFENCE_DATA_FOLDER names,
/// which is how checks point the product at a scratch folder.
/// </remarks>
public sealed class UserDataFolder : IDataFolder
{
    private const string _overrideVariable = "COMMITFENCE_DATA_FOLDER";
    private const string _productFolderName = "CommitFence";

    /// <inheritdoc />
    public string RootFolder
    {
        get { return FindRootFolder(); }
    }

    private static string FindRootFolder()
    {
        var overridden = Environment.GetEnvironmentVariable(_overrideVariable);
        if(!string.IsNullOrWhiteSpace(overridden))
            return Path.GetFullPath(overridden);

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if(string.IsNullOrEmpty(local))
            local = Path.GetTempPath();

        return Path.Combine(local, _productFolderName);
    }
}