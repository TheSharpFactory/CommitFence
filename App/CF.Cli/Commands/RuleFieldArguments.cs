using CF.Core.Findings;
using CF.Core.Rules;

namespace CF.Cli.Commands;

/// <summary>
/// The parts of a rule that rules add and rules set take as options, as typed. It only carries them; the service judges
/// them.
/// </summary>
internal sealed class RuleFieldArguments
{
    public string? Language { get; set; }

    public string? ArtifactType { get; set; }

    public Severity? Severity { get; set; }

    public string? Title { get; set; }

    public List<ConditionDraft>? Where { get; set; }

    public List<ConditionDraft>? Require { get; set; }

    public bool Forbid { get; set; }

    public List<string>? Paths { get; set; }

    public string? Message { get; set; }

    public string? Why { get; set; }

    public bool NoWhere { get; set; }

    public bool NoPaths { get; set; }

    public bool NoMessage { get; set; }

    public bool NoWhy { get; set; }

    public bool IsEmpty
    {
        get { return Language == null && ArtifactType == null && Severity == null && Title == null && Where == null && Require == null && !Forbid && Paths == null && Message == null && Why == null && !NoWhere && !NoPaths && !NoMessage && !NoWhy; }
    }

    public RuleDraft ToDraft(string ruleId)
    {
        return new RuleDraft(
            ruleId,
            Language,
            ArtifactType ?? string.Empty,
            Severity ?? Core.Findings.Severity.Block,
            Title ?? string.Empty,
            Where ?? new List<ConditionDraft>(),
            Require ?? new List<ConditionDraft>(),
            Forbid,
            Paths ?? new List<string>(),
            Message,
            Why);
    }

    public RuleChange ToChange()
    {
        return new RuleChange(
            Title,
            ArtifactType,
            NoWhere ? new List<ConditionDraft>() : Where,
            Require,
            Forbid ? true : null,
            NoPaths ? new List<string>() : Paths,
            NoMessage ? string.Empty : Message,
            NoWhy ? string.Empty : Why);
    }
}