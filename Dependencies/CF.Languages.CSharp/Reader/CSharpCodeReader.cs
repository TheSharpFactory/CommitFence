using System.Globalization;
using CF.Core.Code;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// Reads C# with Roslyn's parser alone: no build, no project, no meaning. Code switched by #if is read with the symbols
/// it is given, or with none defined.
/// </summary>
internal sealed class CSharpCodeReader : ICodeReader
{
    private static readonly CSharpParseOptions _plainOptions = new CSharpParseOptions(LanguageVersion.Latest);
    private static readonly CSharpParseOptions _scriptOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse, SourceCodeKind.Script);

    public IReadOnlyList<Artifact> Read(CodeFile file)
    {
        return ReadWith(file, _plainOptions);
    }

    public IReadOnlyList<Artifact> ReadPiece(CodeFile file)
    {
        var whole = CSharpSyntaxTree.ParseText(file.Content, _plainOptions, file.Path);
        if(!HasSyntaxErrors(whole))
            return WalkArtifacts(file, whole);

        var part = CSharpSyntaxTree.ParseText(file.Content, _scriptOptions, file.Path);
        if(HasSyntaxErrors(part))
            RejectSyntaxErrors(whole);

        return WalkArtifacts(file, part);
    }

    public IReadOnlyList<Artifact> Read(CodeFile file, IReadOnlyList<string> definedSymbols)
    {
        return ReadWith(file, _plainOptions.WithPreprocessorSymbols(definedSymbols));
    }

    public bool HasConditionalSections(CodeFile file)
    {
        var tree = CSharpSyntaxTree.ParseText(file.Content, _plainOptions, file.Path);
        var directives = tree.GetRoot().DescendantTrivia(descendIntoTrivia: true).Where(trivia => trivia.IsDirective).Select(trivia => trivia.GetStructure());

        return directives.OfType<BranchingDirectiveTriviaSyntax>().Any(directive => !directive.BranchTaken);
    }

    private static IReadOnlyList<Artifact> ReadWith(CodeFile file, CSharpParseOptions options)
    {
        var tree = CSharpSyntaxTree.ParseText(file.Content, options, file.Path);
        RejectSyntaxErrors(tree);

        return WalkArtifacts(file, tree);
    }

    private static IReadOnlyList<Artifact> WalkArtifacts(CodeFile file, SyntaxTree tree)
    {
        var walker = new ArtifactWalker(file, new SourceLines(tree));
        walker.Visit(tree.GetRoot());
        return walker.Artifacts;
    }

    private static bool HasSyntaxErrors(SyntaxTree tree)
    {
        return tree.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    private static void RejectSyntaxErrors(SyntaxTree tree)
    {
        var error = tree.GetDiagnostics().FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        if(error == null)
            return;

        var position = error.Location.GetLineSpan().StartLinePosition;
        var message = error.GetMessage(CultureInfo.InvariantCulture);
        throw new CodeReadException(message, position.Line + 1, position.Character + 1);
    }
}