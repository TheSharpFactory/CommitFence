using CF.App.Shell;
using CF.Core.Ports;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Dashboard;

/// <summary>The local push history and the day and rules being shown.</summary>
internal sealed partial class DashboardViewModel(IStatsService? stats, OpenedFolder folder, Action<string> openRule, Action reviewNow)
    : ObservableObject, IScreenModel
{
    private const int _firstNever = 3;
    private const int _firstBroken = 7;

    private List<DashboardRuleRow> _never = [];
    private List<DashboardRuleRow> _broken = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Day), nameof(DayLabel), nameof(DayDescription), nameof(DayCounts))]
    public partial int DayIndex { get; private set; }

    [ObservableProperty]
    public partial bool BusyRing { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMoreNever), nameof(NeverRows))]
    public partial bool AllNever { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMoreBroken), nameof(BrokenRows))]
    public partial bool AllBroken { get; private set; }

    public StatsReport? Report { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool HasReviews { get { return !IsLoading && Report?.Oldest != null; } }
    public bool IsEmpty { get { return !IsLoading && Report?.Oldest == null && Report?.ReadError == null; } }
    public bool HasNotice { get { return !IsLoading && Notice != null; } }
    public bool CanReadAgain { get { return Report?.ReadError != null; } }
    public string? Notice
    {
        get
        {
            var unreadable = Report?.UnreadableNotice;
            if(Report?.ReadError == null)
                return unreadable;

            var warning = Report.ReadErrorNotice;
            return unreadable == null ? warning : warning + "\n" + unreadable;
        }
    }
    public string? Period
    {
        get { return HasReviews ? Report?.Period : null; }
    }
    public IReadOnlyList<StatsDay> Days { get { return Report?.Days ?? []; } }
    public StatsDay? Day { get { return Days.Count == 0 ? null : Days[DayIndex]; } }
    public string? DayLabel { get { return Day?.Label(DayIndex == Days.Count - 1); } }
    public string DayDescription
    {
        get
        {
            if(Day is not { } day)
                return string.Empty;

            return day.Total == 0 ? $"{DayLabel}. No review." : $"{DayLabel}. {day.ReviewsText}: {day.CountsText()}.";
        }
    }

    public IReadOnlyList<StatsReviewCount> DayCounts { get { return Day?.Counts() ?? []; } }
    public IReadOnlyList<StatsReviewCount> Counts { get; private set; } = [];
    public string ChartName { get; private set; } = string.Empty;
    public string Verdict { get; private set; } = string.Empty;
    public string TimingLine { get; private set; } = string.Empty;
    public bool HasTiming { get { return TimingLine.Length != 0; } }
    public IReadOnlyList<DashboardTimeBand> Bands { get; private set; } = [];
    public string SlowestLine { get; private set; } = string.Empty;
    public bool HasSlowest { get { return SlowestLine.Length != 0; } }
    public bool HasNever { get { return _never.Count != 0; } }
    public string NeverSentence { get; private set; } = string.Empty;
    public bool HasMoreNever { get { return !AllNever && _never.Count > _firstNever; } }
    public bool HasMoreBroken { get { return !AllBroken && _broken.Count > _firstBroken; } }
    public bool HasBroken { get { return _broken.Count != 0; } }
    public string? BrokenNote { get; private set; }
    public string NeverButton { get { return $"Show all {_never.Count}"; } }
    public string BrokenButton { get { return $"Show all {_broken.Count}"; } }
    public IReadOnlyList<DashboardRuleRow> NeverRows { get { return AllNever ? _never : _never.Take(_firstNever).ToList(); } }
    public IReadOnlyList<DashboardRuleRow> BrokenRows { get { return AllBroken ? _broken : _broken.Take(_firstBroken).ToList(); } }

    public static string VerdictFor(StatsDay totals)
    {
        if(totals.Total == 0)
            return totals.ReviewsText;

        var didNotPass = totals.Refused + totals.Incomplete;
        if(didNotPass == 0)
            return $"{totals.ReviewsText} allowed";

        var outcome = totals.Refused == 0 ? "incomplete" : totals.Incomplete == 0 ? "blocked" : "blocked or incomplete";
        return didNotPass == totals.Total ? $"{totals.ReviewsText} {outcome}" : $"{didNotPass} of {totals.ReviewsText} {outcome}";
    }

    public void ChooseDay(int day)
    {
        if(Days.Count > 0)
            DayIndex = Math.Clamp(day, 0, Days.Count - 1);
    }

    public void OpenRule(DashboardRuleRow? row)
    {
        if(row?.CanOpen == true)
            openRule(row.Id);
    }

    IRelayCommand IScreenModel.AgainCommand { get { return AgainCommand; } }

    [RelayCommand]
    private async Task AgainAsync()
    {
        IsLoading = true;
        OnPropertyChanged(string.Empty);
        using var delay = new CancellationTokenSource();
        try
        {
            var read = Task.Run(() => stats?.Read(folder.FolderPath));
            if(await Task.WhenAny(read, Task.Delay(400, delay.Token)) != read)
                BusyRing = true;

            Use(await read);
        }
        catch(GitException)
        {
            Use(null);
        }
        finally
        {
            await delay.CancelAsync();
            BusyRing = false;
            IsLoading = false;
            OnPropertyChanged(string.Empty);
        }
    }

    [RelayCommand]
    private void ReviewNow()
    {
        reviewNow();
    }

    [RelayCommand]
    private void ShowNever()
    {
        AllNever = true;
    }

    [RelayCommand]
    private void ShowBroken()
    {
        AllBroken = true;
    }

    private void Use(StatsReport? report)
    {
        Report = report;
        ChooseDay(Days.Count - 1);
        AllNever = false;
        AllBroken = false;
        if(report == null)
            return;

        var totals = report.Totals;
        Counts = totals.Counts();
        var all = totals.Total == 0 ? totals.ReviewsText : $"{totals.ReviewsText}: {totals.CountsText()}";
        var scale = DashboardChartLayout.ScaleLabel(DashboardChartLayout.ScaleTop(Days));
        ChartName = $"Reviews by day, the last 30 days, {char.ToLowerInvariant(scale[0]) + scale[1..]}. {all}. Left and Right choose a day.";
        Verdict = VerdictFor(totals);
        TimingLine = DashboardTimeBand.Summary(report.Timing);
        Bands = DashboardTimeBand.Of(report.Timing);
        SlowestLine = DashboardTimeBand.Slowest(report.Timing);
        NeverSentence = report.NeverFired.Count == 0 ? report.NeverFiredSentence.TrimEnd('.') :
            $"**{report.NeverFired.Count}** of the **{report.RulesInForce}** rules in force found nothing in 30 days";
        _never = report.NeverFired.Select(rule => new DashboardRuleRow(rule.Id, rule, 0, 0)).ToList();
        var counts = report.MostBroken;
        var maximum = counts.Count == 0 || counts.All(row => row.Count == counts[0].Count) ? 0 : counts[0].Count;
        _broken = counts.Select(row => new DashboardRuleRow(row.Id, row.Rule, row.Count, maximum)).ToList();
        BrokenNote = counts.Count == 0 ? null : $"{counts.Count} {(counts.Count == 1 ? "rule had" : "rules had")} findings";
    }
}
