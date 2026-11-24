using CF.Core.Code;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// The facts of one project and framework whose log was closed correctly: what the meaning analyzer wrote, as it wrote
/// it (kept so that the answers can be kept for later), and what it says.
/// </summary>
internal sealed class UnitFacts
{
    public UnitFacts(string partKey, IReadOnlyList<string> factLines, string closingLine, IReadOnlyList<MeaningDeclaration> declarations, IReadOnlyList<MeaningUse> uses)
    {
        PartKey = partKey;
        FactLines = factLines;
        ClosingLine = closingLine;
        Declarations = declarations;
        Uses = uses;
    }

    /// <summary>
    /// The project file relative to the tree, a bar, and the target framework.
    /// </summary>
    public string PartKey { get; }

    public IReadOnlyList<string> FactLines { get; }

    public string ClosingLine { get; }

    public IReadOnlyList<MeaningDeclaration> Declarations { get; }

    public IReadOnlyList<MeaningUse> Uses { get; }
}