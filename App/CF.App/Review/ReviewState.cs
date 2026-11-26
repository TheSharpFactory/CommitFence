using CF.Core.Findings;
using CF.Core.Review;

namespace CF.App.Review;

/// <summary>
/// What the page showed for one kind of review, kept while the program runs so that moving the choice back shows it again.
/// </summary>
internal sealed class ReviewState
{
    public required VerdictViewModel Verdict { get; init; }

    public required bool HasResult { get; init; }

    public required IReadOnlyList<FileGroupViewModel> FileGroups { get; init; }

    public required IReadOnlyList<PanelFileViewModel> PanelFiles { get; init; }

    public required string PanelHead { get; init; }

    public required string CleanFilesText { get; init; }

    public required string FilesLine { get; init; }

    public required string OpenAllText { get; init; }

    public required IReadOnlyList<RuleRowViewModel> RuleRows { get; init; }

    public required string RulesHead { get; init; }

    public required string? ChosenRule { get; init; }

    public required string ShowAllText { get; init; }

    public required string FolderCleanLead { get; init; }

    public required string FolderCleanNames { get; init; }

    public required ReviewResult? Result { get; init; }

    public required Dictionary<Finding, RuleInfo> RuleInfos { get; init; }

    public required string ResultTarget { get; init; }

    public required string ResultLabel { get; init; }

    public required bool StartsOpen { get; init; }

    public required IReadOnlyList<ReasonViewModel> Reasons { get; init; }

    public required string ReasonsLine { get; init; }

    public required bool AllReasonsShown { get; init; }

    public required IReadOnlyList<string> CleanPushFiles { get; init; }

    public required string CleanPushLine { get; init; }

    public required string CleanPushMore { get; init; }
}
