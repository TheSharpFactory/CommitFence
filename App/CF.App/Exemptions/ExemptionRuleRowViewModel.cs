using System.Globalization;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Exemptions;

/// <summary>
/// One row of the panel "By rule": the rule's source mark, its title, how many exemptions name it, its id, and a neutral bar with a
/// red part for the exemptions that give no reason. A click chooses the rule, or shows all again when it is the chosen one.
/// </summary>
internal sealed partial class ExemptionRuleRowViewModel : ObservableObject
{
    private readonly Action<ExemptionRuleRowViewModel> _choose;

    public ExemptionRuleRowViewModel(ExemptionRuleCount count, ExemptionRuleInfo rule, int most, Action<ExemptionRuleRowViewModel> choose)
    {
        Count = count.Count;
        WithoutReason = count.WithoutReason;
        Rule = rule;
        Fraction = (double)count.Count / most;
        RedFraction = (double)count.WithoutReason / most;
        _choose = choose;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsChosen { get; set; }

    /// <summary>
    /// Another rule is chosen: only the bar steps back; the words, the number and the red stay as they were.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSteppedBack { get; set; }

    public ExemptionRuleInfo Rule { get; }

    public string RuleId
    {
        get { return Rule.Id; }
    }

    /// <summary>
    /// The rule's title; the id stands in its place for a rule the repository does not have.
    /// </summary>
    public string Title
    {
        get { return Rule.HasTitle ? Rule.Title : Rule.Id; }
    }

    public bool ShowsId
    {
        get { return Rule.HasTitle; }
    }

    public int Count { get; }

    public int WithoutReason { get; }

    public string CountText
    {
        get { return Count.ToString(CultureInfo.InvariantCulture); }
    }

    /// <summary>
    /// The rule's count over the largest count, from 0 to 1.
    /// </summary>
    public double Fraction { get; }

    /// <summary>
    /// The part of the bar with no reason, over the largest count.
    /// </summary>
    public double RedFraction { get; }

    public bool HasWithoutReason
    {
        get { return WithoutReason > 0; }
    }

    /// <summary>
    /// "1 without a reason": the number in words beside a bar with a red part.
    /// </summary>
    public string WithoutReasonText
    {
        get { return $"{WithoutReason.ToString(CultureInfo.InvariantCulture)} without a reason"; }
    }

    public bool IsCustom
    {
        get { return Rule.IsCustom; }
    }

    public bool IsFromFile
    {
        get { return Rule.IsFromFile; }
    }

    public string SpokenName
    {
        get
        {
            var source = Rule.IsCustom ? "custom rule" : (Rule.IsFromFile ? "rule of .editorconfig" : "rule the repository does not have");
            var red = HasWithoutReason ? $", {WithoutReasonText}" : string.Empty;
            var state = IsChosen ? ", chosen: press to show all again" : string.Empty;
            return $"{Title}, {Rule.Id}, {Count} {(Count == 1 ? "exemption" : "exemptions")}{red}, {source}{state}";
        }
    }

    [RelayCommand]
    private void Choose()
    {
        _choose(this);
    }
}
