using System.Globalization;
using CF.App.Parts;
using CF.Core.Code;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Exemptions;

/// <summary>
/// One exemption: the rule's id, its reason (or "No reason given", which is a finding for an allow comment), the rule's title and the
/// form of a pragma or an attribute, its line, and, opened, the lines around it with "Open at line" and "Open the rule".
/// </summary>
internal sealed partial class ExemptionRowViewModel : ExemptionListItem
{
    private readonly Action<ExemptionRowViewModel> _openLine;
    private readonly Action<string> _openRule;

    public ExemptionRowViewModel(ExemptionEntry entry, ExemptionRuleInfo rule, bool startsOpen, Action<ExemptionRowViewModel> openLine, Action<string> openRule)
    {
        Entry = entry;
        Rule = rule;
        _openLine = openLine;
        _openRule = openRule;
        IsOpen = startsOpen;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsOpen { get; set; }

    public ExemptionRuleInfo Rule { get; }

    public ExemptionEntry Entry { get; }

    public string RuleId
    {
        get { return Entry.RuleId; }
    }

    /// <summary>
    /// The lines around the exemption, from the commit, the exemption's own line marked. They are coloured when the row is first drawn
    /// open, not when the list is laid out.
    /// </summary>
    public IReadOnlyList<FileLineViewModel> Lines
    {
        get { return field ??= ExemptionCode.Lines(Entry); }
    }

    /// <summary>
    /// A finding: an allow comment with no reason. Its title is the stop sign and "No reason given".
    /// </summary>
    public bool IsFinding
    {
        get { return Entry.IsFinding; }
    }

    /// <summary>
    /// A pragma or an attribute with no reason: "No reason given" in the quiet type, with no mark (it is not a finding).
    /// </summary>
    public bool HasQuietNoReason
    {
        get { return !Entry.IsFinding && string.IsNullOrWhiteSpace(Entry.Reason); }
    }

    public bool HasReason
    {
        get { return !string.IsNullOrWhiteSpace(Entry.Reason); }
    }

    public string Reason
    {
        get { return Entry.Reason ?? string.Empty; }
    }

    public string NoReason
    {
        get { return HasReason ? string.Empty : ExemptionWords.NoReason; }
    }

    public string FindingLead
    {
        get { return IsFinding ? "A finding: an exemption needs a reason" : string.Empty; }
    }

    public bool HasRuleTitle
    {
        get { return Rule.HasTitle; }
    }

    public string RuleTitle
    {
        get { return Rule.Title; }
    }

    /// <summary>
    /// "#pragma warning disable" or "[SuppressMessage]"; none for the allow comment, which stands in the code when the row opens.
    /// </summary>
    public string? FormText
    {
        get { return ExemptionWords.FormOf(Entry.Form); }
    }

    public bool HasForm
    {
        get { return FormText != null; }
    }

    public string LineText
    {
        get { return "line " + Entry.Line.ToString(CultureInfo.InvariantCulture); }
    }

    public string OpenLineText
    {
        get { return "Open at line " + Entry.Line.ToString(CultureInfo.InvariantCulture); }
    }

    /// <summary>
    /// What a finding asks for: the reason, written after the rule, with the code to write marked as code.
    /// </summary>
    public string HintMarked
    {
        get { return $"Write why after the rule: `// {AllowComment.Marker} {Entry.RuleId}: ` and the reason. Until then this line is a finding of its own."; }
    }

    public bool CanOpenRule
    {
        get { return Rule.IsKnown; }
    }

    public string SpokenName
    {
        get
        {
            var reason = Entry.IsFinding ? $"{ExemptionWords.NoReason}, a finding" : ExemptionWords.ReasonOf(Entry);
            return $"{Entry.RuleId}, {reason}, line {Entry.Line.ToString(CultureInfo.InvariantCulture)}, {(IsOpen ? "expanded" : "folded")}";
        }
    }

    [RelayCommand]
    private void Toggle()
    {
        IsOpen = !IsOpen;
    }

    [RelayCommand]
    private void OpenLine()
    {
        _openLine(this);
    }

    [RelayCommand]
    private void OpenRule()
    {
        _openRule(Entry.RuleId);
    }
}
