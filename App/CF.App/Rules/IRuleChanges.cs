using CF.Core.Findings;
using CF.Core.Rules;

namespace CF.App.Rules;

/// <summary>
/// What a row and a page ask of the list when a person changes a rule: the changes are made through the rule service. The
/// switch and the effect are made at once; the title and the sentence are written together when the person saves.
/// </summary>
internal interface IRuleChanges
{
    /// <summary>
    /// Switches the rule on or off.
    /// </summary>
    void SetEnabled(string ruleId, bool enabled);

    /// <summary>
    /// Makes the rule block the push or only warn.
    /// </summary>
    void SetSeverity(string ruleId, Severity severity);

    /// <summary>
    /// Writes the title and the sentence of a rule in one change. Nothing when it was written; otherwise the message of
    /// the refusal, and the file is as it was.
    /// </summary>
    string? ChangeRule(string ruleId, RuleChange change);

    /// <summary>
    /// Adds a new rule at the end of the rules, or right after the rule named. Nothing when it was added; otherwise the message of the refusal.
    /// </summary>
    string? AddRule(RuleDraft draft, string? afterRuleId);

    /// <summary>
    /// Opens the page of a new rule that is a copy of this one, not saved yet. Saving it puts it right after the rule.
    /// </summary>
    void DuplicateRule(string ruleId);

    /// <summary>
    /// Tries a saved rule as the page holds it, with its edits, on code. Nothing is saved. May be called from any thread.
    /// </summary>
    TryAnswer TryChangedRule(string ruleId, RuleChange change, string code);

    /// <summary>
    /// Tries a rule that is not saved yet on code. Nothing is saved. May be called from any thread.
    /// </summary>
    TryAnswer TryNewRule(RuleDraft draft, string code);

    /// <summary>
    /// Removes the rule, and offers to bring it back.
    /// </summary>
    void RemoveRule(string ruleId);

    /// <summary>
    /// Drops the new rule that was being written.
    /// </summary>
    void DiscardDraft();
}
