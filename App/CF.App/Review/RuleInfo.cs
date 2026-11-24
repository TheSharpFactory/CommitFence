using CF.Core.Findings;

namespace CF.App.Review;

/// <summary>
/// What the Review screen knows about the rule behind a finding: where it comes from, its title and, for a team rule, its
/// sentence. A rule the screen cannot show (a compiler message, a rule that is gone) has only its origin.
/// </summary>
internal sealed class RuleInfo
{
    private static readonly List<SentencePieceViewModel> _noSentence = new List<SentencePieceViewModel>();

    public RuleInfo(FindingSource source, string title, bool canOpen, IReadOnlyList<SentencePieceViewModel>? sentence)
    {
        Source = source;
        Title = title;
        CanOpen = canOpen;
        Sentence = sentence ?? _noSentence;
    }

    public FindingSource Source { get; }

    public string Title { get; }

    /// <summary>
    /// Whether the Rules screen has a page for the rule.
    /// </summary>
    public bool CanOpen { get; }

    public IReadOnlyList<SentencePieceViewModel> Sentence { get; }

    /// <summary>
    /// Where the rule comes from, in the words that begin the plain line of a finding (no full stop: the line goes on).
    /// </summary>
    public string Origin
    {
        get
        {
            switch(Source)
            {
                case FindingSource.CodeRule:
                case FindingSource.StructureRule:
                    return "A team rule, written here";
                case FindingSource.EditorConfig:
                    return "From .editorconfig";
                case FindingSource.Compiler:
                    return "From the compiler";
                default:
                    return "From a rule pack";
            }
        }
    }
}
