using CF.Core.Code;
using CF.Languages.CSharp.Catalog;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// Turns what the build said into the meaning properties of the artifacts the reader found, and into the artifacts that
/// only meaning knows.
/// </summary>
/// <remarks>
/// A class or a method that no part of the build compiled (code switched off by a symbol of another framework, or a
/// file that no project compiles) is left out: nothing true can be said of it.
/// </remarks>
internal sealed class MeaningApplier
{
    private readonly MeaningIndex _index;

    public MeaningApplier(MeaningIndex index)
    {
        _index = index;
    }

    public IReadOnlyList<Artifact> Apply(CodeFile file, IReadOnlyList<Artifact> artifacts)
    {
        var result = new List<Artifact>();
        var replaced = new Dictionary<Artifact, Artifact>();
        foreach(var artifact in artifacts)
        {
            var enriched = Enrich(file, artifact, replaced);
            if(enriched == null)
                continue;

            replaced[artifact] = enriched;
            result.Add(enriched);
        }

        result.AddRange(CreateUsages(file));
        return result;
    }

    private Artifact? Enrich(CodeFile file, Artifact artifact, Dictionary<Artifact, Artifact> replaced)
    {
        var extra = FindMeaningProperties(file, artifact);
        if(extra == null)
            return null;

        var parent = artifact.Parent != null && replaced.TryGetValue(artifact.Parent, out var newParent) ? newParent : artifact.Parent;
        return new Artifact(artifact.Type, artifact.Language, artifact.File, artifact.Span, parent, artifact.Properties.Concat(extra).ToList(), artifact.Reach);
    }

    private List<Property>? FindMeaningProperties(CodeFile file, Artifact artifact)
    {
        if(artifact.Type == CSharpNames.Class)
            return FindClassProperties(file, artifact);

        if(artifact.Type == CSharpNames.Method)
            return FindMethodProperties(file, artifact);

        return new List<Property>();
    }

    private List<Property>? FindClassProperties(CodeFile file, Artifact artifact)
    {
        var declaration = _index.FindDeclaration(file.Path, artifact.Span, "class");
        if(declaration == null)
            return null;

        return new List<Property> { Text(CSharpNames.DeclaredAccess, declaration.Access) };
    }

    private List<Property>? FindMethodProperties(CodeFile file, Artifact artifact)
    {
        var declaration = _index.FindDeclaration(file.Path, artifact.Span, "method");
        if(declaration == null)
            return null;

        return new List<Property>
        {
            Text(CSharpNames.DeclaredAccess, declaration.Access),
            Text(CSharpNames.ContainingTypeDeclaredAccess, declaration.ContainerAccess)
        };
    }

    private List<Artifact> CreateUsages(CodeFile file)
    {
        var usages = new List<Artifact>();
        foreach(var use in _index.FindUses(file.Path))
        {
            foreach(var place in use.Places)
                usages.Add(CreateUsage(file, use, place));
        }

        return usages;
    }

    private static Artifact CreateUsage(CodeFile file, MeaningUse use, SourcePoint place)
    {
        var properties = new List<Property>
        {
            Text(CSharpNames.Target, use.Used),
            Text(CSharpNames.TargetType, use.UsedType),
            Text(CSharpNames.TargetAssembly, use.UsedAssembly)
        };

        return new Artifact(CSharpNames.Usage, file.Language, file.Path, new SourceSpan(place.Line, place.Column, place.Line, place.Column), null, properties);
    }

    private static Property Text(string name, string value)
    {
        return new Property(name, PropertyKind.Meaning, PropertyValue.FromText(value));
    }
}