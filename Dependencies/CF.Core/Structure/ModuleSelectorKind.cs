namespace CF.Core.Structure;

/// <summary>
/// What a module selector names: a namespace, a project, or a folder.
/// </summary>
public enum ModuleSelectorKind
{
    /// <summary>
    /// A namespace and every namespace below it.
    /// </summary>
    Namespace,

    /// <summary>
    /// A project, by the name of the assembly it is compiled into.
    /// </summary>
    Project,

    /// <summary>
    /// A folder, by the path of the file that declares the code, relative to the repository.
    /// </summary>
    Folder
}