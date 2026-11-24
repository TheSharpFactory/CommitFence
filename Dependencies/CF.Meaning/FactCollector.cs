using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CF.Meaning;

/// <summary>
/// Turns one syntax tree and its semantic model into facts: what each declaration is and what each name, and each thing
/// the compiler resolved without a name, refers to.
/// </summary>
internal static class FactCollector
{
    public static IReadOnlyList<string> Collect(SemanticModel model, CancellationToken cancellation)
    {
        var facts = new List<string>();
        var uses = new UseFactWriter(model, cancellation);
        var resolved = new ResolvedUses(model, uses, cancellation);
        foreach(var node in model.SyntaxTree.GetRoot(cancellation).DescendantNodes())
        {
            var declaration = DeclarationFactWriter.Write(model, node, cancellation);
            if(declaration != null)
                facts.Add(declaration);

            if(node is SimpleNameSyntax name)
                uses.Add(name);

            resolved.Add(node);
        }

        facts.AddRange(uses.ToFacts());
        return facts;
    }
}