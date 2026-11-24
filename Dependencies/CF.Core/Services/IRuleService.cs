using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;
using CF.Core.Rules.Settings;

namespace CF.Core.Services;

/// <summary>
/// The rules of a settings file: what the command rules list and rules show do, and what the Rules screen shows.
/// </summary>
public interface IRuleService
{
    /// <summary>
    /// Every rule of the settings file, in the order of the file. Throws a <see cref="SettingsException" /> when the
    /// file is missing or wrong.
    /// </summary>
    IReadOnlyList<RuleDefinition> ListRules(string settingsPath);

    /// <summary>
    /// Everything the settings file holds: its rules and its structure rules. Throws a <see cref="SettingsException" />
    /// when the file is missing or wrong.
    /// </summary>
    RuleSettings ReadSettings(string settingsPath);

    /// <summary>
    /// The value of the requires key alone, read before the rest of the file is interpreted.
    /// </summary>
    /// <remarks>
    /// Nothing when the file is missing, cannot be read, is broken or has no readable value: the strict read of the
    /// file says what is wrong.
    /// </remarks>
    string? ReadRequires(string settingsPath);

    /// <summary>
    /// Everything the text of a settings file holds. Throws a <see cref="SettingsException" /> when the text is wrong.
    /// </summary>
    RuleSettings ParseSettings(string text);

    /// <summary>
    /// The rule with this identifier, or nothing when the file has none. Throws a <see cref="SettingsException" /> when
    /// the file is missing or wrong.
    /// </summary>
    RuleDefinition? ShowRule(string settingsPath, string ruleId);

    /// <summary>
    /// The rules the repository's own files set for the languages' tools, one listing for each language that has
    /// such files. The repository is the folder of the settings file. Throws a <see cref="RuleChangeException" /> when
    /// such a file exists and cannot be read.
    /// </summary>
    IReadOnlyList<FileRuleListing> ListFileRules(string settingsPath);

    /// <summary>
    /// The rule of a repository file with this identifier (case does not matter), or nothing. Throws a
    /// <see cref="RuleChangeException" /> when such a file exists and cannot be read.
    /// </summary>
    FileRule? ShowFileRule(string settingsPath, string ruleId);

    /// <summary>
    /// What the languages offer to rules: the artifact types and their properties, with value types and whether they
    /// need the build.
    /// </summary>
    /// <remarks>
    /// Given a language, only that one; given a type as well, only that type. Throws a
    /// <see cref="RuleChangeException" /> for a name the product does not have.
    /// </remarks>
    IReadOnlyList<LanguageCatalog> ListCatalogs(string? languageId, string? artifactTypeName);

    /// <summary>
    /// Switches a rule on or off.
    /// </summary>
    /// <remarks>
    /// A rule that is off stays in the file but is not run in a review. Every change below is checked as the strict
    /// reader checks a file, before anything is written; a refused change throws a <see cref="RuleChangeException" />
    /// and leaves the file as it was.
    /// </remarks>
    void SetEnabled(string settingsPath, string ruleId, bool enabled);

    /// <summary>
    /// Makes a rule block the push or only warn.
    /// </summary>
    void SetSeverity(string settingsPath, string ruleId, Severity severity);

    /// <summary>
    /// Adds a rule at the end of the rules. It is on.
    /// </summary>
    void AddRule(string settingsPath, RuleDraft draft);

    /// <summary>
    /// Adds a rule just before the rule named, so that it stands at that place. It is on. The rule named must be one of the
    /// team's rules; an id that is not there is refused as every rule command refuses it.
    /// </summary>
    void AddRuleBefore(string settingsPath, RuleDraft draft, string beforeRuleId);

    /// <summary>
    /// Adds a rule just after the rule named. It is on. The rule named must be one of the team's rules.
    /// </summary>
    void AddRuleAfter(string settingsPath, RuleDraft draft, string afterRuleId);

    /// <summary>
    /// Adds, right after a rule, a copy of it with a new id: the same sentence, severity, switch, paths, message and why, and
    /// the title "Copy of" and the rule's title. Refused for a new id that exists or is malformed, for a rule that is not
    /// there and for a rule of <c>.editorconfig</c>.
    /// </summary>
    void CopyRule(string settingsPath, string ruleId, string newRuleId);

    /// <summary>
    /// Changes parts of a rule.
    /// </summary>
    void ChangeRule(string settingsPath, string ruleId, RuleChange change);

    /// <summary>
    /// Removes a rule.
    /// </summary>
    void RemoveRule(string settingsPath, string ruleId);

    /// <summary>
    /// Runs a saved rule as it would stand after a change, on source text, saving nothing and taking no lock: what a page
    /// shows for a rule with edits that are not saved yet.
    /// </summary>
    /// <remarks>
    /// The change is built and judged exactly as <see cref="ChangeRule" /> would build and judge it, so an edit that a save
    /// would refuse is refused here with the same <see cref="RuleChangeException" />.
    /// </remarks>
    RuleTryResult TryChangedRule(string settingsPath, string ruleId, RuleChange change, string sourceText, string? fileName);

    /// <summary>
    /// Runs a rule that is not saved yet, as <see cref="AddRule" /> would add it, on source text, saving nothing and taking
    /// no lock. A draft that adding would refuse is refused here with the same <see cref="RuleChangeException" />.
    /// </summary>
    RuleTryResult TryNewRule(string settingsPath, RuleDraft draft, string sourceText, string? fileName);

    /// <summary>
    /// Runs a rule on source text and says, for each artifact of the rule's type, whether the code breaks the rule,
    /// follows it, or is not covered by it.
    /// </summary>
    /// <remarks>
    /// The file name is only used for the rule's paths; nothing means they are not applied. A rule that needs meaning
    /// is not tried.
    /// </remarks>
    RuleTryResult TryRule(string settingsPath, string ruleId, string sourceText, string? fileName);
}