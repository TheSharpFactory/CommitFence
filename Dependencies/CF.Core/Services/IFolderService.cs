namespace CF.Core.Services;

/// <summary>
/// Says what a folder is to the product: where its settings file is and which repository it belongs to.
/// </summary>
public interface IFolderService
{
    /// <summary>
    /// Describes the folder. The settings file is the one of that name in the folder itself, as the command line finds it.
    /// A folder that is not in a repository is described too, with no branch.
    /// </summary>
    OpenedFolder Open(string folder);
}
