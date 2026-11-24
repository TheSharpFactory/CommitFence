using CF.Core.Code;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// Joins the facts of every project and framework that answered into the answers for the tree.
/// </summary>
/// <remarks>
/// The same file compiled for two frameworks, or into two projects, says the same thing twice: a declaration is kept
/// once for each assembly (the same linked file in two projects is two types), and a use is kept once with all its
/// places. A project sees the types of the projects it references only as a library, so the file that declares such a
/// type is found from the declarations of the project that compiled it.
/// </remarks>
internal static class MeaningAnswerBuilder
{
    private const string _languageId = "csharp";

    private static readonly string[] _typeKinds = { "class", "struct", "record", "interface", "enum", "delegate" };

    public static MeaningAnswers Build(IReadOnlyList<UnitFacts> units, IReadOnlyList<string> gaps, IReadOnlyList<string> treeAssemblies)
    {
        var declarations = new Dictionary<string, MeaningDeclaration>(StringComparer.Ordinal);
        var uses = new Dictionary<string, MeaningUse>(StringComparer.Ordinal);
        foreach(var unit in units)
        {
            foreach(var declaration in unit.Declarations)
                declarations.TryAdd(DeclarationKey(declaration), declaration);

            foreach(var use in unit.Uses)
                AddUse(uses, use);
        }

        var files = FindTypeFiles(declarations.Values);
        var parts = units.Select(unit => unit.PartKey).Distinct(StringComparer.Ordinal).ToList();
        return new MeaningAnswers(_languageId, declarations.Values.ToList(), uses.Values.Select(use => WithTypeFile(use, files)).ToList(), parts, gaps, treeAssemblies);
    }

    private static string DeclarationKey(MeaningDeclaration declaration)
    {
        return string.Join("\u001f", declaration.Assembly, declaration.File, declaration.Start.Line, declaration.Start.Column, declaration.Kind);
    }

    private static Dictionary<string, string> FindTypeFiles(IEnumerable<MeaningDeclaration> declarations)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach(var declaration in declarations.Where(candidate => _typeKinds.Contains(candidate.Kind)).OrderBy(candidate => candidate.File, StringComparer.Ordinal))
            files.TryAdd(TypeKey(declaration.Assembly, declaration.Name), declaration.File);

        return files;
    }

    private static string TypeKey(string assembly, string typeName)
    {
        return assembly + "\u001f" + typeName;
    }

    private static MeaningUse WithTypeFile(MeaningUse use, Dictionary<string, string> files)
    {
        if(use.UsedFile.Length > 0 || !files.TryGetValue(TypeKey(use.UsedAssembly, use.UsedType), out var file))
            return use;

        return new MeaningUse(use.File, use.Member, use.MemberNamespace, use.MemberAssembly, use.Used, use.UsedType, use.UsedNamespace, use.UsedAssembly, file, use.Places);
    }

    private static void AddUse(Dictionary<string, MeaningUse> uses, MeaningUse use)
    {
        var key = string.Join("\u001f", use.File, use.Member, use.Used, use.UsedType, use.UsedAssembly, use.MemberAssembly);
        if(!uses.TryGetValue(key, out var known))
        {
            uses[key] = use;
            return;
        }

        var places = known.Places.Concat(use.Places).DistinctBy(place => (place.Line, place.Column)).OrderBy(place => place.Line).ThenBy(place => place.Column).ToList();
        uses[key] = new MeaningUse(known.File, known.Member, known.MemberNamespace, known.MemberAssembly, known.Used, known.UsedType, known.UsedNamespace, known.UsedAssembly, known.UsedFile, places);
    }
}