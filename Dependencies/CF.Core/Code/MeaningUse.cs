namespace CF.Core.Code;

/// <summary>
/// What a name in a file really refers to, and where, as the compiler resolved it: a member of a type or a type, used
/// by a member of the code.
/// </summary>
public sealed class MeaningUse
{
    /// <summary>
    /// Creates a use.
    /// </summary>
    public MeaningUse(
        string file,
        string member,
        string memberNamespace,
        string memberAssembly,
        string used,
        string usedType,
        string usedNamespace,
        string usedAssembly,
        string usedFile,
        IReadOnlyList<SourcePoint> places)
    {
        File = file;
        Member = member;
        MemberNamespace = memberNamespace;
        MemberAssembly = memberAssembly;
        Used = used;
        UsedType = usedType;
        UsedNamespace = usedNamespace;
        UsedAssembly = usedAssembly;
        UsedFile = usedFile;
        Places = places;
    }

    /// <summary>
    /// The file the use is in, relative to the repository, with forward slashes.
    /// </summary>
    public string File { get; }

    /// <summary>
    /// The member that uses, as the compiler writes it; empty when the use is outside any member.
    /// </summary>
    public string Member { get; }

    /// <summary>
    /// The namespace of that member.
    /// </summary>
    public string MemberNamespace { get; }

    /// <summary>
    /// The assembly that member is compiled into.
    /// </summary>
    public string MemberAssembly { get; }

    /// <summary>
    /// What is used: a member written as Type.Member, or a type.
    /// </summary>
    public string Used { get; }

    /// <summary>
    /// The type used, or the type that holds the member used.
    /// </summary>
    public string UsedType { get; }

    /// <summary>
    /// The namespace of that type.
    /// </summary>
    public string UsedNamespace { get; }

    /// <summary>
    /// The assembly of that type.
    /// </summary>
    public string UsedAssembly { get; }

    /// <summary>
    /// The file that declares that type, relative to the repository, or empty when it comes from a library.
    /// </summary>
    public string UsedFile { get; }

    /// <summary>
    /// Every place in the file where the name is written, in the order of the file.
    /// </summary>
    public IReadOnlyList<SourcePoint> Places { get; }
}