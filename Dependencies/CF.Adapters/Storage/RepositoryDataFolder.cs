using CF.Core.Ports;

namespace CF.Adapters.Storage;

/// <summary>
/// Names the folder of one repository inside the data folder: the repository's folder name and a short hash of where
/// its git folder is.
/// </summary>
/// <remarks>
/// This is so that two repositories of the same name do not share a private copy.
/// </remarks>
internal static class RepositoryDataFolder
{
    public static string For(string rootFolder, RepositoryLocation repository)
    {
        var name = Path.GetFileName(repository.WorkFolder.TrimEnd('/', '\\'));
        var safeName = string.Concat(name.Select(letter => char.IsLetterOrDigit(letter) || letter == '-' || letter == '_' ? letter : '_'));

        return Path.Combine(rootFolder, $"{safeName}-{PathHash.Of(repository.GitDirectory)}");
    }
}