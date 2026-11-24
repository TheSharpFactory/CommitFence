using CF.Core.Code;
using CF.Languages.CSharp.Toolchain;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// The answers for a tree, laid out to be found by file: the declarations by where they start, and the uses of each
/// file.
/// </summary>
internal sealed class MeaningIndex
{
    private static readonly IReadOnlyList<MeaningUse> _noUses = new List<MeaningUse>();

    private readonly Dictionary<string, Dictionary<(int Line, int Column), MeaningDeclaration>> _declarations = new Dictionary<string, Dictionary<(int Line, int Column), MeaningDeclaration>>(ToolchainPaths.Comparer);
    private readonly Dictionary<string, List<MeaningUse>> _uses = new Dictionary<string, List<MeaningUse>>(ToolchainPaths.Comparer);

    public MeaningIndex(MeaningAnswers answers)
    {
        foreach(var declaration in answers.Declarations)
            AddDeclaration(declaration);

        foreach(var use in answers.Uses)
            AddUse(use);
    }

    /// <summary>
    /// The declaration of this kind that starts here in the file, or nothing when no part of the build compiled it.
    /// </summary>
    public MeaningDeclaration? FindDeclaration(string file, SourceSpan span, string kind)
    {
        if(!_declarations.TryGetValue(file, out var inFile))
            return null;

        inFile.TryGetValue((span.StartLine, span.StartColumn), out var declaration);
        return declaration?.Kind == kind ? declaration : null;
    }

    public IReadOnlyList<MeaningUse> FindUses(string file)
    {
        return _uses.TryGetValue(file, out var uses) ? uses : _noUses;
    }

    private void AddDeclaration(MeaningDeclaration declaration)
    {
        if(!_declarations.TryGetValue(declaration.File, out var inFile))
        {
            inFile = new Dictionary<(int Line, int Column), MeaningDeclaration>();
            _declarations[declaration.File] = inFile;
        }

        inFile.TryAdd((declaration.Start.Line, declaration.Start.Column), declaration);
    }

    private void AddUse(MeaningUse use)
    {
        if(!_uses.TryGetValue(use.File, out var inFile))
        {
            inFile = new List<MeaningUse>();
            _uses[use.File] = inFile;
        }

        inFile.Add(use);
    }
}