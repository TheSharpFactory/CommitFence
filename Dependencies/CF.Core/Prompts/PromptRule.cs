using CF.Core.Code;
using CF.Core.Rules;

namespace CF.Core.Prompts;

/// <summary>
/// What the prompt says about the rule behind a finding: its title and, for a team rule, its sentence. A rule of the
/// repository's own file has a title at most, and no sentence.
/// </summary>
public sealed class PromptRule
{
    /// <summary>
    /// Creates what is known of a rule.
    /// </summary>
    public PromptRule(string title, string? sentence)
    {
        Title = title;
        Sentence = sentence;
    }

    /// <summary>
    /// The title, or an empty text when the rule has none.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// The rule as one sentence, or nothing when the rule has none.
    /// </summary>
    public string? Sentence { get; }

    /// <summary>
    /// A team rule: its title and its sentence.
    /// </summary>
    public static PromptRule Of(RuleDefinition rule)
    {
        return new PromptRule(rule.Title, RuleSentence.Of(rule));
    }

    /// <summary>
    /// A rule of the repository's own file: its title, when it has one.
    /// </summary>
    public static PromptRule Of(FileRule rule)
    {
        return new PromptRule(rule.Title ?? string.Empty, null);
    }
}