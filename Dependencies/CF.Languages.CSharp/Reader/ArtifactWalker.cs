using CF.Core.Code;
using CF.Languages.CSharp.Catalog;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// Walks the syntax tree of one file and makes an artifact of every if statement, method and class. The parent of an
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
        Enter(CSharpNames.Class, node, ClassProperties.Create(node, _lines), ArtifactReaches.ForClass(node, _lines));
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