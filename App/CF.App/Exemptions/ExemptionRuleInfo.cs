using CF.App.Review;
using CF.Core.Code;
using CF.Core.Rules;

namespace CF.App.Exemptions;

/// <summary>
/// What the screen knows about a rule an exemption names: whether the repository has it as a team rule (a custom rule) or from
/// .editorconfig, its title, its sentence (a team rule) or its line in the file (a rule of the file). A rule the repository does not
/// have is known by its id alone.
/// </summary>
internal sealed class ExemptionRuleInfo
{
    private static readonly List<SentencePieceViewModel> _noSentence = new List<SentencePieceViewModel>();

    private ExemptionRuleInfo(string id, string title, ExemptionRuleSource source, IReadOnlyList<SentencePieceViewModel> sentence, int? fileLine)
    {
        Id = id;
        Title = title;
        Source = source;
        Sentence = sentence;
        FileLine = fileLine;
    }

    public string Id { get; }

    public string Title { get; }

    public ExemptionRuleSource Source { get; }

    /// <summary>
    /// A team rule's sentence in pieces; none for the other kinds.
    /// </summary>
    public IReadOnlyList<SentencePieceViewModel> Sentence { get; }

    /// <summary>
    /// The line of .editorconfig that sets a rule of the file, when it says one.
    /// </summary>
    public int? FileLine { get; }

    public bool IsKnown
    {
        get { return Source != ExemptionRuleSource.Unknown; }
    }

    public bool IsCustom
    {
        get { return Source == ExemptionRuleSource.Custom; }
    }

    public bool IsFromFile
    {
        get { return Source == ExemptionRuleSource.File; }
    }

    public bool HasTitle
    {
        get { return Title.Length > 0; }
    }

    public static ExemptionRuleInfo Unknown(string id)
    {
        return new ExemptionRuleInfo(id, string.Empty, ExemptionRuleSource.Unknown, _noSentence, null);
    }

    public static ExemptionRuleInfo Team(RuleDefinition rule)
    {
        return new ExemptionRuleInfo(rule.Id, rule.Title, ExemptionRuleSource.Custom, SentencePieceViewModel.Of(rule), null);
    }

    public static ExemptionRuleInfo File(FileRule rule)
    {
        return new ExemptionRuleInfo(rule.Id, rule.Title ?? string.Empty, ExemptionRuleSource.File, _noSentence, rule.ListedPlace?.Line);
    }
}

/// <summary>
/// Where a rule an exemption names lives.
/// </summary>
internal enum ExemptionRuleSource
{
    /// <summary>The repository does not have the rule.</summary>
    Unknown,

    /// <summary>A team rule of commitfence.json.</summary>
    Custom,

    /// <summary>A rule of .editorconfig.</summary>
    File
}
