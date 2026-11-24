namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The notes of the meaning analyzer in one compiler log, as the texts it wrote: the facts, and the closing entries
/// (there should be exactly one).
/// </summary>
/// <remarks>
/// They are told from the compiler's own diagnostics by their rule identifiers.
/// </remarks>
internal sealed class SarifMeaningNotes
{
    private const string _factRuleId = "CFM001";
    private const string _closingRuleId = "CFM999";

    private SarifMeaningNotes(IReadOnlyList<string> facts, IReadOnlyList<string> closings)
    {
        Facts = facts;
        Closings = closings;
    }

    public IReadOnlyList<string> Facts { get; }

    public IReadOnlyList<string> Closings { get; }

    /// <summary>
    /// Whether this rule identifier is one of the analyzer's: such a result is never a finding.
    /// </summary>
    public static bool IsMeaningRule(string? ruleId)
    {
        return ruleId == _factRuleId || ruleId == _closingRuleId;
    }

    public static SarifMeaningNotes From(SarifRunDto run)
    {
        var results = run.Results ?? new List<SarifResultDto>();
        var facts = results.Where(result => result.RuleId == _factRuleId).Select(result => result.Message?.Text ?? string.Empty).ToList();
        var closings = results.Where(result => result.RuleId == _closingRuleId).Select(result => result.Message?.Text ?? string.Empty).ToList();

        return new SarifMeaningNotes(facts, closings);
    }
}