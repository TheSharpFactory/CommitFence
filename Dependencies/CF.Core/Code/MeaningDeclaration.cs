namespace CF.Core.Code;

/// <summary>
/// What the compiler says a declaration is, found where the declaration starts in its file.
/// </summary>
public sealed class MeaningDeclaration
{
    /// <summary>
    /// Creates a declaration. The access words are those of the language's catalog.
    /// </summary>
    public MeaningDeclaration(string file, SourcePoint start, string kind, string name, string assembly, string access, string containerAccess)
    {
        File = file;
        Start = start;
        Kind = kind;
        Name = name;
        Assembly = assembly;
        Access = access;
        ContainerAccess = containerAccess;
    }

    /// <summary>
    /// The path of the file, relative to the repository, with forward slashes.
    /// </summary>
    public string File { get; }

    /// <summary>
    /// Where the declaration starts in the file.
    /// </summary>
    public SourcePoint Start { get; }

    /// <summary>
    /// What it declares, such as class or method.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Its name: a type as its full name, a method as the compiler writes it.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The assembly the declaration is compiled into.
    /// </summary>
    public string Assembly { get; }

    /// <summary>
    /// The access it really has, with the language's defaults applied and every part of a partial type taken together.
    /// </summary>
    public string Access { get; }

    /// <summary>
    /// The access of the type it is declared in, or none.
    /// </summary>
    public string ContainerAccess { get; }
}