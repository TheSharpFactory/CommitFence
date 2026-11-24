namespace CF.Core.Code;

/// <summary>
/// What a language's build said about one tree: what every declaration is and what every name refers to, for the parts
/// of the build that answered completely, and a plain sentence for each part that did not.
/// </summary>
/// <remarks>
/// Only an answer without gaps may be judged; a gap means the rules that need meaning are not run.
/// </remarks>
public sealed class MeaningAnswers
{
    /// <summary>
    /// Creates answers.
    /// </summary>
    /// <remarks>
    /// The parts are the keys of the parts of the build that answered, which the same language is asked for again when
    /// the tree before a push is needed. The tree assemblies are the assemblies built from the source of the tree,
    /// whether this review compiled them or not.
    /// </remarks>
    public MeaningAnswers(string language, IReadOnlyList<MeaningDeclaration> declarations, IReadOnlyList<MeaningUse> uses, IReadOnlyList<string> parts, IReadOnlyList<string> gaps, IReadOnlyList<string> treeAssemblies)
    {
        Language = language;
        Declarations = declarations;
        Uses = uses;
        Parts = parts;
        Gaps = gaps;
        TreeAssemblies = treeAssemblies;
    }

    /// <summary>
    /// The identifier of the language, such as csharp.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// What the declarations are.
    /// </summary>
    public IReadOnlyList<MeaningDeclaration> Declarations { get; }

    /// <summary>
    /// What the names refer to.
    /// </summary>
    public IReadOnlyList<MeaningUse> Uses { get; }

    /// <summary>
    /// The keys of the parts of the build that answered.
    /// </summary>
    public IReadOnlyList<string> Parts { get; }

    /// <summary>
    /// A sentence for each part that should have answered and did not, or for the whole when nothing could be asked.
    /// </summary>
    public IReadOnlyList<string> Gaps { get; }

    /// <summary>
    /// The assemblies built from the source of the tree.
    /// </summary>
    /// <remarks>
    /// A used type of one of them, whose file is not known, is declared in code this review did not compile; a used
    /// type of any other assembly comes from a library. Empty when the answers are for the tree before a push, which is
    /// never asked where a type lives.
    /// </remarks>
    public IReadOnlyList<string> TreeAssemblies { get; }

    /// <summary>
    /// Whether every part answered.
    /// </summary>
    public bool IsComplete
    {
        get { return Gaps.Count == 0; }
    }

    /// <summary>
    /// Answers that hold nothing and say why.
    /// </summary>
    public static MeaningAnswers Missing(string language, string reason)
    {
        return new MeaningAnswers(language, new List<MeaningDeclaration>(), new List<MeaningUse>(), new List<string>(), new List<string> { reason }, new List<string>());
    }
}