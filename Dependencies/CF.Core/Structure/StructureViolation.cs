using CF.Core.Code;

namespace CF.Core.Structure;

/// <summary>
/// One use that the layer directions do not allow.
/// </summary>
/// <remarks>
/// Its identity is the direction, the file and the member that use, the type used and that type's assembly:
/// it survives lines moving, and a second forbidden use in another member is another violation.
/// </remarks>
public sealed class StructureViolation
{
    /// <summary>
    /// Creates a violation.
    /// </summary>
    public StructureViolation(string language, string fromModule, string toModule, MeaningUse use, SourcePoint place)
    {
        Language = language;
        FromModule = fromModule;
        ToModule = toModule;
        Use = use;
        Place = place;
    }

    /// <summary>
    /// The identifier of the language of the code, such as csharp.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// The module whose code uses.
    /// </summary>
    public string FromModule { get; }

    /// <summary>
    /// The module whose code is used.
    /// </summary>
    public string ToModule { get; }

    /// <summary>
    /// The use.
    /// </summary>
    public MeaningUse Use { get; }

    /// <summary>
    /// Where the use is written: its first place in the file.
    /// </summary>
    public SourcePoint Place { get; }

    /// <summary>
    /// The identity, as one text.
    /// </summary>
    public string Identity
    {
        get { return IdentityInFile(Use.File); }
    }

    internal string IdentityInFile(string path)
    {
        return string.Join("\u001f", FromModule, ToModule, path, Use.Member, Use.UsedType, Use.UsedAssembly);
    }
}
