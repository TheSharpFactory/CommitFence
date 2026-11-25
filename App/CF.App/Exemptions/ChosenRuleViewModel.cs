using CF.App.Review;
using CF.Core.Services;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Exemptions;

/// <summary>
/// "The rule chosen": the tag of its source, its id and title, the rule as its sentence (a rule of the file says where the file sets
/// it), how many lines set it aside, and the one button that acts on the rule: "Narrow the rule" for a custom rule, "Open the rule" for
/// a rule of the file.
/// </summary>
internal sealed partial class ChosenRuleViewModel
{
    private readonly Action _act;

    public ChosenRuleViewModel(ExemptionRuleInfo rule, IReadOnlyList<ExemptionEntry> exemptions, Action act)
    {
        Rule = rule;
        _act = act;
        Exemptions = exemptions;
    }

    public ExemptionRuleInfo Rule { get; }

    public IReadOnlyList<ExemptionEntry> Exemptions { get; }

    public string RuleId
    {
        get { return Rule.Id; }
    }

    public string Title
    {
        get { return Rule.Title; }
    }

    public bool HasTitle
    {
        get { return Rule.HasTitle; }
    }

    public bool IsCustom
    {
        get { return Rule.IsCustom; }
    }

    public bool IsFromFile
    {
        get { return Rule.IsFromFile; }
    }

    public string TagText
    {
        get { return Rule.IsCustom ? "Custom rule" : ".editorconfig rule"; }
    }

    public IReadOnlyList<SentencePieceViewModel> Sentence
    {
        get { return Rule.Sentence; }
    }

    public bool HasSentence
    {
        get { return Rule.IsCustom; }
    }

    /// <summary>
    /// "Set in .editorconfig, line 378." for a rule of the file.
    /// </summary>
    public string SetIn
    {
        get { return Rule.FileLine is { } line ? $"Set in .editorconfig, line {line}." : "Set in .editorconfig."; }
    }

    public bool HasSetIn
    {
        get { return Rule.IsFromFile; }
    }

    /// <summary>
    /// How many lines set the rule aside, and what to do about it.
    /// </summary>
    public string Aside
    {
        get
        {
            return Rule.IsCustom
                ? ExemptionCounts.SetAside(Exemptions, "it") + " Narrow the rule opens it on Rules with a new condition, so it stops reaching code it should leave alone."
                : (Rule.IsFromFile ? ExemptionCounts.SetAside(Exemptions, "it", false) + " Open the rule to see where the file sets it." : ExemptionCounts.SetAside(Exemptions, "it", false));
        }
    }

    /// <summary>
    /// What stands on the Rules screen under the rule's title when it is narrowed from here.
    /// </summary>
    public string StripText
    {
        get { return $"**From Exemptions.** {ExemptionCounts.SetAside(Exemptions, "this rule")} Add a condition so it stops reaching them; nothing is saved until Save rule."; }
    }

    public bool HasButton
    {
        get { return Rule.IsKnown; }
    }

    public string ButtonText
    {
        get { return Rule.IsCustom ? "Narrow the rule" : "Open the rule"; }
    }

    [RelayCommand]
    private void Act()
    {
        _act();
    }
}
