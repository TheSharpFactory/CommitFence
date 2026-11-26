using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Prompts;

namespace CF.App.Review;

/// <summary>
/// What the Review screen knows about the rule behind a finding: where it comes from, its title and, for a team rule, its
/// sentence. A rule the screen cannot show (a compiler message, a rule that is gone) has only its origin.
/// </summary>
internal sealed class RuleInfo
{
    private static readonly List<SentencePieceViewModel> _noSentence = new List<SentencePieceViewModel>();

    public RuleInfo(FindingSource source, string title, bool canOpen, IReadOnlyList<SentencePieceViewModel>? sentence, PromptRule? prompt = null, FileRule? fileRule = null, string? why = null)
    {
        FileRule = fileRule;
        Source = source;
        Title = title;
        CanOpen = canOpen;
        Sentence = sentence ?? _noSentence;
        Prompt = prompt;
        Why = why ?? string.Empty;
    }

    /// <summary>
    /// Why the team has the rule, for a team rule that says; empty otherwise.
    /// </summary>
    public string Why { get; }

    public FileRule? FileRule { get; }

    public FindingSource Source { get; }

    public string Title { get; }

    /// <summary>
    /// Whether the Rules screen has a page for the rule.
    /// </summary>
    public bool CanOpen { get; }

    public IReadOnlyList<SentencePieceViewModel> Sentence { get; }

    /// <summary>
    /// What the prompt for a finding of this rule says about the rule, when the rule is known.
    /// </summary>
    public PromptRule? Prompt { get; }

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
                case FindingSource.Exemption:
                    return "From an allow comment";
                default:
                    return "From a rule pack";
            }
        }
    }
}