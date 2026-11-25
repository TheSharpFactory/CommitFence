using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Rules.Settings;

namespace CF.Core.Services;

/// <summary>Turns a repository's local history into the numbers for Dashboard and stats.</summary>
public sealed class StatsService(IGit git, IClock clock, IReviewHistory history, IRuleService rules) : IStatsService
{
    private const int _days = 30;
    private static readonly Dictionary<string, StatsRule> _productFindings = new Dictionary<string, StatsRule>(StringComparer.OrdinalIgnoreCase)
    {
        [StatsRule.LayerDirectionId] = new StatsRule(StatsRule.LayerDirectionId, "A use breaks a direction", false, Severity.Block),
        [StatsRule.MissingExemptionReasonId] = new StatsRule(StatsRule.MissingExemptionReasonId, "An exemption needs a reason", false, Severity.Block)
    };

    /// <inheritdoc />
    public StatsReport? Read(string folder)
    {
        if(git.FindRepository(folder) is not { } repository)
            return null;

        var today = DateOnly.FromDateTime(clock.Now.Date);
        var first = today.AddDays(1 - _days);
        var stored = history.Read(repository);
        var local = stored.Records.Select(record => new LocalReview(record, clock.ToLocalTime(record.EndedAt))).ToList();
        var recent = local.Where(review => review.Date >= first && review.Date <= today).ToList();
        var oldest = local.Count == 0 ? (DateOnly?)null : local.Min(review => review.Date);
        var days = DaysFrom(first, recent);
        var recorded = recent.SelectMany(review => review.Record.Findings)
            .GroupBy(finding => finding.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(finding => (long)finding.Count), StringComparer.OrdinalIgnoreCase);
        var known = CurrentRules(SettingsFileName.PathIn(repository.WorkFolder));
        var byId = known.ToLookup(rule => rule.Id, StringComparer.OrdinalIgnoreCase);
        var broken = recorded.Select(pair => new StatsFindingCount(pair.Key, pair.Value,
                _productFindings.TryGetValue(pair.Key, out var productRule) ? productRule : byId[pair.Key].FirstOrDefault()))
            .OrderByDescending(row => row.Count).ThenBy(row => row.Id, StringComparer.Ordinal).ToList();
        var active = known.Where(rule => rule.Effect != null).ToList();
        var never = active.Where(rule => !recorded.ContainsKey(rule.Id)).ToList();

        return new StatsReport(days, oldest, stored.UnreadableLines, StatsTimings.From(recent.Select(review =>
            (new StatsReviewTime(review.EndedAt, review.Record.DurationMilliseconds, review.Record.FilesReviewed), review.Record.Full)).ToList()),
            broken, never, active.Count, stored.ReadError);
    }

    private static List<StatsDay> DaysFrom(DateOnly first, List<LocalReview> recent)
    {
        var byDay = recent.ToLookup(review => review.Date, review => review.Record.Outcome);
        return Enumerable.Range(0, _days).Select(first.AddDays).Select(day =>
        {
            var totals = byDay[day].ToLookup(outcome => outcome);
            return new StatsDay(day, totals[RecordedReviewOutcome.Blocked].Count(), totals[RecordedReviewOutcome.Incomplete].Count(),
                totals[RecordedReviewOutcome.Warned].Count() + totals[RecordedReviewOutcome.Clean].Count());
        }).ToList();
    }

    private List<StatsRule> CurrentRules(string settingsPath)
    {
        var current = new List<StatsRule>();
        try
        {
            current.AddRange(rules.ListRules(settingsPath).Select(rule =>
                new StatsRule(rule.Id, rule.Title, false, rule.Enabled ? rule.Severity : null)));
        }
        catch(SettingsException)
        {
            // The recorded findings still have identifiers when the working settings cannot be read.
        }

        try
        {
            current.AddRange(rules.ListFileRules(settingsPath).SelectMany(listing => listing.Rules)
                .OrderBy(rule => rule.Id, StringComparer.Ordinal).Select(rule => new StatsRule(rule.Id, rule.Title, true,
                    rule.ListedPlace is { State: FileRuleState.On } place ? place.Severity : null)));
        }
        catch(Exception exception) when(exception is RuleChangeException or SettingsException)
        {
            // The history can also be read before the current file rules are available.
        }

        return current;
    }

    private sealed record LocalReview(ReviewRecord Record, DateTimeOffset EndedAt)
    {
        public DateOnly Date { get { return DateOnly.FromDateTime(EndedAt.Date); } }
    }
}
