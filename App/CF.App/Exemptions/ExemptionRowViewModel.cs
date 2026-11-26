using System.Globalization;
using CF.App.Parts;
using CF.Core.Code;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Exemptions;

/// <summary>
/// The exemptions of one rule with the same reason in a file, with an excerpt around the first line.
/// </summary>
internal sealed partial class ExemptionRowViewModel : ExemptionListItem
{
    private const int _initialActions = 6;
    private readonly IReadOnlyList<ExemptionEntry> _entries;
    private readonly List<ExemptionEntry> _lines;
    private readonly Action<ExemptionEntry> _openLine;
    private readonly Action<string> _openRule;
    private bool _showsAllLines;

    public ExemptionRowViewModel(IReadOnlyList<ExemptionEntry> entries, ExemptionRuleInfo rule, bool startsOpen, Action<ExemptionEntry> openLine, Action<string> openRule)
    {
        _entries = entries;
        _lines = entries.DistinctBy(entry => entry.Line).ToList();
        Rule = rule;
        IsFinding = entries[0].IsFinding;
        _openLine = openLine;
        _openRule = openRule;
        IsOpen = startsOpen;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsOpen { get; set; }

    public ExemptionRuleInfo Rule { get; }

    public string RuleId
    {
        get { return Rule.Id; }
    }

    public IReadOnlyList<FileLineViewModel> ExcerptLines
    {
        get { return field ??= ExemptionCode.Lines(_entries[0]); }
    }

    public string HintMarked
    {
        get { return $"**This line is a finding:** an exemption needs a reason. Write why after the rule: `// {AllowComment.Marker} {RuleId}: ` and the reason."; }
    }

    public IReadOnlyList<ExemptionRowAction> Actions
    {
        get { return field ??= MakeActions(); }
        private set;
    }

    /// <summary>
    /// A finding: an allow comment with no reason. Its title is the stop sign and "No reason given".
    /// </summary>
    public bool IsFinding { get; }

    /// <summary>
    /// A pragma or an attribute with no reason: "No reason given" in the quiet type, with no mark (it is not a finding).
    /// </summary>
    public bool HasQuietNoReason
    {
        get { return !IsFinding && !HasReason; }
    }

    public bool HasReason
    {
        get { return !string.IsNullOrWhiteSpace(Reason); }
    }

    public string Reason
    {
        get { return _entries[0].Reason ?? string.Empty; }
    }

    public string NoReason
    {
        get { return HasReason ? string.Empty : ExemptionWords.NoReason; }
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
    /// The forms used by directives or attributes. Allow comments stand in the code when the row opens.
    /// </summary>
    public string? FormText
    {
        get
        {
            if(_entries.Count == 1)
                return ExemptionWords.FormOf(_entries[0].Form);

            var forms = string.Join(", ", _entries.Select(entry => ExemptionWords.FormOf(entry.Form)).OfType<string>().Distinct(StringComparer.Ordinal));
            return forms.Length == 0 ? null : forms;
        }
    }

    public bool HasForm
    {
        get { return FormText != null; }
    }

    public string LineText
    {
        get { return HasMultipleLines ? CountText + " lines" : "line " + LineNumber(_entries[0]); }
    }

    public bool HasMultipleLines
    {
        get { return _lines.Count > 1; }
    }

    public string LinesMeta
    {
        get { return HasMultipleLines ? "Lines " + LineNumbers : string.Empty; }
    }

    private string CountText
    {
        get { return _lines.Count.ToString(CultureInfo.InvariantCulture); }
    }

    private string LineNumbers
    {
        get
        {
            if(_lines.Count > _initialActions)
                return $"{string.Join(", ", _lines.Take(3).Select(LineNumber))} and {(_lines.Count - 3).ToString(CultureInfo.InvariantCulture)} more";

            return $"{string.Join(", ", _lines.Take(_lines.Count - 1).Select(LineNumber))} and {LineNumber(_lines[^1])}";
        }
    }

    public string SpokenName
    {
        get
        {
            var reason = IsFinding ? $"{ExemptionWords.NoReason}, a finding" : ExemptionWords.ReasonOf(_entries[0]);
            var lines = HasMultipleLines ? $"{LineText}: {LineNumbers}" : LineText;
            return $"{RuleId}, {reason}, {lines}, {(IsOpen ? "expanded" : "folded")}";
        }
    }

    private static string LineNumber(ExemptionEntry entry)
    {
        return entry.Line.ToString(CultureInfo.InvariantCulture);
    }

    private List<ExemptionRowAction> MakeActions()
    {
        var showMore = !_showsAllLines && _lines.Count > _initialActions;
        var entries = showMore ? _lines.Take(_initialActions) : _lines;
        var actions = entries.Select(entry => new ExemptionRowAction(
            "Open at line " + LineNumber(entry), new RelayCommand(() => _openLine(entry)), OpensLine: true)).ToList();
        if(showMore)
            actions.Add(new ExemptionRowAction($"Show all {CountText} lines", ShowAllLinesCommand));
        if(Rule.IsKnown)
            actions.Add(new ExemptionRowAction("Open the rule", OpenRuleCommand, OpensRule: true));

        return actions;
    }

    [RelayCommand]
    private void ShowAllLines()
    {
        _showsAllLines = true;
        Actions = MakeActions();
        OnPropertyChanged(nameof(Actions));
    }

    [RelayCommand]
    private void Toggle()
    {
        IsOpen = !IsOpen;
    }

    [RelayCommand]
    private void OpenRule()
    {
        _openRule(RuleId);
    }
}
