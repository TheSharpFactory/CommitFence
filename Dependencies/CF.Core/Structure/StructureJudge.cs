using CF.Core.Code;

namespace CF.Core.Structure;

/// <summary>
/// Judges what the code of one tree really uses against the layer directions.
/// </summary>
/// <remarks>
/// An unused using is not a use, and a fully qualified name needs no using: the facts come from what the compiler
/// resolved. Code that belongs to no module is not judged.
/// </remarks>
public sealed class StructureJudge
{
    private readonly StructureRules _rules;

    /// <summary>
    /// Creates a judge for these rules.
    /// </summary>
    public StructureJudge(StructureRules rules)
    {
        _rules = rules;
    }

    /// <summary>
    /// The violations in the answers, one for each identity, found at the first place of the use.
    /// </summary>
    public IReadOnlyList<StructureViolation> Judge(MeaningAnswers answers)
    {
        var found = new Dictionary<string, StructureViolation>(StringComparer.Ordinal);
        foreach(var use in answers.Uses)
        {
            var violation = JudgeUse(answers.Language, use);
            if(violation != null)
                Keep(found, violation);
        }

        return found.Values.ToList();
    }

    /// <summary>
    /// The uses that cannot be judged: the type used is declared in a project of the tree that this review did not
    /// compile, so its file is unknown, and a module named by folder could hold it.
    /// </summary>
    /// <remarks>
    /// One for each identity of a violation. Judged as if it were not there, such a use would be a pass that was not
    /// earned.
    /// </remarks>
    public IReadOnlyList<MeaningUse> FindUnplaced(MeaningAnswers answers)
    {
        return answers.Uses.Where(use => IsUnplaced(answers, use)).DistinctBy(use => (use.File, use.Member, use.UsedType, use.UsedAssembly)).ToList();
    }

    private static void Keep(Dictionary<string, StructureViolation> found, StructureViolation violation)
    {
        if(!found.TryGetValue(violation.Identity, out var known) || IsEarlier(violation.Place, known.Place))
            found[violation.Identity] = violation;
    }

    private static bool IsEarlier(SourcePoint first, SourcePoint second)
    {
        return first.Line < second.Line || (first.Line == second.Line && first.Column < second.Column);
    }

    private bool IsUnplaced(MeaningAnswers answers, MeaningUse use)
    {
        if(use.UsedFile.Length > 0 || !answers.TreeAssemblies.Contains(use.UsedAssembly, StringComparer.Ordinal))
            return false;

        var from = _rules.FindModule(use.MemberNamespace, use.MemberAssembly, use.File);
        return from != null && _rules.IsPlacementOpen(from, use.UsedNamespace, use.UsedAssembly);
    }

    private StructureViolation? JudgeUse(string language, MeaningUse use)
    {
        var from = _rules.FindModule(use.MemberNamespace, use.MemberAssembly, use.File);
        var to = _rules.FindModule(use.UsedNamespace, use.UsedAssembly, use.UsedFile);
        if(from == null || to == null || _rules.Directions.Permits(from, to))
            return null;

        var place = use.Places.Count > 0 ? use.Places[0] : new SourcePoint(1, 1);
        return new StructureViolation(language, from, to, use, place);
    }
}