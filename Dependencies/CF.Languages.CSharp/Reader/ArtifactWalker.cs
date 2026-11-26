using CF.Core.Code;
using CF.Languages.CSharp.Catalog;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// Walks the syntax tree and makes an artifact for the file and for each if statement, method and class. The parent of an
/// artifact is the nearest artifact around it.
/// </summary>
internal sealed class ArtifactWalker : CSharpSyntaxWalker
{
    private readonly CodeFile _file;
    private readonly SourceLines _lines;
    private readonly List<Artifact> _artifacts = new List<Artifact>();
    private readonly Stack<Artifact> _parents = new Stack<Artifact>();

    public ArtifactWalker(CodeFile file, SourceLines lines)
    {
        _file = file;
        _lines = lines;
    }

    public IReadOnlyList<Artifact> Artifacts
    {
        get { return _artifacts; }
    }

    public override void VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var span = _lines.SpanBetween(node.FullSpan);
        var reach = new ArtifactReach(span, [], null) { IsWholeFile = true };
        var properties = new PropertyList()
            .Syntax(CSharpNames.Name, Path.GetFileName(_file.Path))
            .Syntax(CSharpNames.TypesInFile, CountTypes(node.Members))
            .ToList();
        _artifacts.Add(new Artifact(CSharpNames.File, _file.Language, _file.Path, span, null, properties, reach));
        base.VisitCompilationUnit(node);
    }

    private static int CountTypes(SyntaxList<MemberDeclarationSyntax> members)
    {
        var count = 0;
        foreach(var member in members)
        {
            if(member is BaseNamespaceDeclarationSyntax space)
                count += CountTypes(space.Members);
            else if(member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
                count++;
        }

        return count;
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        Enter(CSharpNames.IfStatement, node, IfStatementProperties.Create(node, _lines), ArtifactReaches.ForIfStatement(node, _lines));
        base.VisitIfStatement(node);
        _parents.Pop();
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        Enter(CSharpNames.Method, node, MethodProperties.Create(node, _lines), ArtifactReaches.ForMethod(node, _lines));
        base.VisitMethodDeclaration(node);
        _parents.Pop();
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        Enter(CSharpNames.Class, node, ClassProperties.Create(node, _lines, Path.GetFileNameWithoutExtension(_file.Path)), ArtifactReaches.ForClass(node, _lines));
        base.VisitClassDeclaration(node);
        _parents.Pop();
    }

    private void Enter(string type, SyntaxNode node, IReadOnlyList<Property> properties, ArtifactReach reach)
    {
        var parent = _parents.Count == 0 ? null : _parents.Peek();
        var artifact = new Artifact(type, _file.Language, _file.Path, reach.Own, parent, properties, reach);
        _artifacts.Add(artifact);
        _parents.Push(artifact);
    }
}