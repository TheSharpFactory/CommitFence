using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CF.App.Dashboard;
using CF.App.Parts;
using CF.App.Review;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Services;
using Xunit;

namespace CF.App.Tests;

/// <summary>The Dashboard presents history and uses the ordinary Review and Rules entry points.</summary>
public sealed class DashboardScreenTests
{
    [Theory]
    [InlineData(11, 6, 10, 2)]
    [InlineData(41, 6, 10, 2)]
    [InlineData(27, 11, 15, 3)]
    [InlineData(24, 16, 20, 3)]
    [InlineData(39, 23, 30, 3)]
    [InlineData(11, 11, 15, 2)]
    public void TheChartUsesTheFourthBusiestDayAndDrawsRefusalsBeforeIncompleteAndAllowed(
        int busiest, int fourth, long top, int kindsDrawn)
    {
        var date = new DateOnly(2026, 11, 24);
        var day = new StatsDay(date, 9, 2, busiest - 11);
        var days = new List<StatsDay> { new StatsDay(date, 0, 0, fourth), day, day, day, new StatsDay(date, 0, 0, 1) };
        Assert.Equal(top, DashboardChartLayout.ScaleTop(days));
        var drawn = DashboardChartLayout.UsesBlocks(top) ?
            DashboardChartLayout.BlocksOf(day, top, 280).Select(block => block.Kind).ToList() :
            DashboardChartLayout.BoxesOf(day, top).Select(group => group.Kind).ToList();
        Assert.Equal(new[] { ReviewOutcome.Blocked, ReviewOutcome.Incomplete, ReviewOutcome.Clean }.Take(kindsDrawn), drawn);
    }

    [Fact]
    public void ABusyDayIsOneBoxPerReviewUpToTheTopLineWithTheRestCountedAboveIt()
    {
        var date = new DateOnly(2026, 11, 24);
        var busy = new StatsDay(date, 9, 2, 30);
        var boxes = DashboardChartLayout.BoxesOf(busy, 10);
        Assert.Equal(new List<(ReviewOutcome, int)> { (ReviewOutcome.Blocked, 9), (ReviewOutcome.Incomplete, 1) }, boxes.Select(group => (group.Kind, group.Count)));
        Assert.Equal(276, DashboardChartLayout.ColumnHeight(busy, 10, 280), 6);
        Assert.Equal("31 more", DashboardChartLayout.TotalText(busy, 10));
        Assert.Equal("41 reviews", DashboardChartLayout.TotalText(busy, 30));
        Assert.Equal("41 reviews", busy.ReviewsText);

        var blocks = DashboardChartLayout.BlocksOf(busy, 30, 280);
        Assert.Equal(new List<double> { 58.8, 13.1, 196.1 }, blocks.Select(block => Math.Round(block.Height, 1)));
        Assert.Equal(276, DashboardChartLayout.ColumnHeight(busy, 30, 280), 6);
        Assert.Equal(268, blocks.Sum(block => block.Height), 6);

        var lopsided = DashboardChartLayout.BlocksOf(new StatsDay(date, 1, 1, 100), 30, 240);
        Assert.All(lopsided, block => Assert.True(block.Height >= 4));
        Assert.Equal(236, DashboardChartLayout.ColumnHeight(new StatsDay(date, 1, 1, 100), 30, 240), 6);

        var days = Enumerable.Range(0, 30).Select(day => new StatsDay(date.AddDays(day - 29), 0, 0, 2)).ToList();
        days[29] = busy;
        days[28] = new StatsDay(days[28].Date, 0, 0, 25);
        var frame = new ChartFrame(1000, 30);
        var totals = DashboardChartLayout.TotalLabels(days, 10, frame, TextWidth);
        var kept = Assert.Single(totals);
        Assert.Equal((29, "31 more"), (kept.Day, kept.Text));
        Assert.Equal(1000 - 49, kept.Left, 6);

        days[28] = new StatsDay(days[28].Date, 0, 0, 2);
        days[10] = new StatsDay(days[10].Date, 0, 0, 25);
        Assert.Equal(new List<int> { 10, 29 }, DashboardChartLayout.TotalLabels(days, 10, frame, TextWidth).Select(label => label.Day));

        var narrow = new ChartFrame(400, 30);
        days[0] = new StatsDay(days[0].Date, 0, 0, 42);
        days[1] = new StatsDay(days[1].Date, 0, 0, 25);
        days[28] = new StatsDay(days[28].Date, 0, 0, 25);
        var edgeLabels = DashboardChartLayout.TotalLabels(days, 10, narrow, TextWidth);
        Assert.Equal([0, 10, 29], edgeLabels.Select(label => label.Day));
        Assert.Equal(narrow.BoxLeft(0) + narrow.Box / 2, edgeLabels[0].Anchor);
        Assert.Null(edgeLabels[1].Anchor);
        Assert.Equal(narrow.BoxLeft(29) + narrow.Box / 2, edgeLabels[2].Anchor);
    }

    [Fact]
    public void TheAxisKeepsFewLinesAndSmallBlocksNeverPaintOverEachOther()
    {
        Assert.Equal(new List<long> { 5, 10 }, DashboardChartLayout.AxisLines(10));
        Assert.Equal(new List<long> { 10, 20 }, DashboardChartLayout.AxisLines(20));
        Assert.Equal(new List<long> { 150, 300 }, DashboardChartLayout.AxisLines(300));

        var date = new DateOnly(2026, 11, 24);
        var small = DashboardChartLayout.BlocksOf(new StatsDay(date, 1, 1, 1), 300, 240);
        Assert.Equal(new List<double> { 4.0, 4.0, 4.0 }, small.Select(block => block.Height));
        Assert.Equal(20, DashboardChartLayout.ColumnHeight(new StatsDay(date, 1, 1, 1), 300, 240));

        var full = new StatsDay(date, 1, 1, 298);
        var blocks = DashboardChartLayout.BlocksOf(full, 300, 240);
        Assert.All(blocks, block => Assert.True(block.Height >= 4));
        Assert.True(DashboardChartLayout.ColumnHeight(full, 300, 240) <= 236 + 1e-9);
    }

    [AvaloniaFact]
    public void TheDatesUnderTheAxisGiveTheChosenDayItsPlaceAndTheChartsLineWraps()
    {
        var days = DashboardPictures.Month().Days;
        var wide = new ChartFrame(1000, 30);
        var narrow = new ChartFrame(400, 30);
        Assert.Equal(new List<string> { "26 Oct", "2 Nov", "9 Nov", "16 Nov", "Today" }, TextsOf(DashboardChartLayout.DateLabels(days, 29, wide, TextWidth)));
        Assert.DoesNotContain("Today", TextsOf(DashboardChartLayout.DateLabels(days, 28, wide, TextWidth)));
        Assert.Contains("23 Nov", TextsOf(DashboardChartLayout.DateLabels(days, 28, wide, TextWidth)));
        Assert.Contains("Today", TextsOf(DashboardChartLayout.DateLabels(days, 27, wide, TextWidth)));
        Assert.DoesNotContain("Today", TextsOf(DashboardChartLayout.DateLabels(days, 25, narrow, TextWidth)));
        Assert.Contains("20 Nov", TextsOf(DashboardChartLayout.DateLabels(days, 25, narrow, TextWidth)));
        Assert.Contains("Today", TextsOf(DashboardChartLayout.DateLabels(days, 22, narrow, TextWidth)));

        var sizes = new List<double>();
        foreach(var chartWidth in new[] { 1000.0, 300.0 })
        {
            var chart = new DashboardChart { Days = days, SelectedDay = 29 };
            using var host = new ViewHost(new StackPanel { Children = { chart } }, chartWidth, 600);
            sizes.Add(chart.DesiredSize.Height);
        }

        Assert.True(sizes[1] >= sizes[0] + 18);
        Assert.Equal(0, (sizes[1] - sizes[0]) % 18, 6);
    }

    private static double TextWidth(string text)
    {
        return text.Length * 7;
    }

    private static List<string> TextsOf(IReadOnlyList<ChartLabel> labels)
    {
        return labels.Select(label => label.Text).ToList();
    }

    [Theory]
    [InlineData(12, 4, 68, "16 of 84 reviews blocked or incomplete")]
    [InlineData(1, 0, 0, "1 review blocked")]
    [InlineData(1, 0, 1, "1 of 2 reviews blocked")]
    [InlineData(0, 1, 0, "1 review incomplete")]
    [InlineData(0, 1, 1, "1 of 2 reviews incomplete")]
    [InlineData(0, 0, 2, "2 reviews allowed")]
    [InlineData(0, 0, 1, "1 review allowed")]
    [InlineData(0, 0, 0, "No review")]
    public void TheVerdictDescribesReviewOutcomesForThePeriod(int refused, int incomplete, int allowed, string expected)
    {
        Assert.Equal(expected, DashboardViewModel.VerdictFor(new StatsDay(default, refused, incomplete, allowed)));
    }

    [AvaloniaFact]
    public async Task TheEmptyDashboardOpensDoctor()
    {
        using var shell = new ShellHost();
        shell.Stats.Report = DashboardPictures.Month() with { Oldest = null };
        shell.Model.CurrentScreen = Screen.Dashboard;
        await shell.Model.Dashboard.AgainCommand.ExecutionTask!;
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 760, Height = 1000 });
        var doctor = host.Find<Button>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == "Open Doctor");
        Assert.NotNull(doctor);
        Assert.True(doctor.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.Enter);
        Assert.Equal(Screen.Doctor, shell.Model.CurrentScreen);
    }

    [AvaloniaFact]
    public async Task MostBrokenShowsAnOffRuleWithItsHistoricalShare()
    {
        using var shell = new ShellHost();
        shell.Stats.Report = DashboardPictures.Month();
        shell.Model.CurrentScreen = Screen.Dashboard;
        var model = shell.Model.Dashboard;
        await model.AgainCommand.ExecutionTask!;
        var view = new DashboardView { DataContext = model };
        using var host = new ViewHost(view, 1352, 1100);

        var rule = Assert.Single(model.BrokenRows, row => row.Id == "IDE0005");
        Assert.Equal(9.0 / 31, rule.Share);
        var row = Assert.IsType<ListBoxItem>(view.BrokenList.ContainerFromIndex(3));
        var content = Assert.Single(row.GetVisualDescendants(), visual =>
            AutomationProperties.GetName(visual) == "IDE0005 Remove unnecessary usings, Off, 9 findings");
        Assert.Contains(content.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Inlines != null &&
            text.Inlines.OfType<Run>().Any(run => run.Text == "Remove unnecessary usings") &&
            text.Inlines.OfType<Run>().Any(run => run.Text == "\u00A0\u00A0Off"));
        Assert.Contains(content.GetVisualDescendants().OfType<EffectMark>(), mark => mark.IsEffectivelyVisible && mark.Kind == EffectKind.Off);
        var bar = Assert.Single(content.GetVisualDescendants().OfType<FractionBar>(), bar => bar.IsEffectivelyVisible);
        Assert.Equal(rule.Share, bar.Fraction);

        model.ShowBrokenCommand.Execute(null);
        host.Pump();
        var removed = Assert.Single(model.BrokenRows, row => row.Id == "OLD001");
        Assert.Equal(1.0 / 31, removed.Share);
        Assert.False(removed.CanOpen);
    }

    [AvaloniaFact]
    public async Task TheChartHasOneKeyboardStopAndShowAllKeepsAllRules()
    {
        using var shell = new ShellHost();
        var report = DashboardPictures.Month();
        shell.Stats.Report = report;
        shell.Model.CurrentScreen = Screen.Dashboard;
        var model = shell.Model.Dashboard;
        await model.AgainCommand.ExecutionTask!;
        var view = new DashboardView { DataContext = model };
        using var host = new ViewHost(view, 1352, 900);
        Assert.Equal("Reviews of pushes on this machine, 26 Oct to 24 Nov", model.Period);
        Assert.Equal("**32** of the **47** rules in force found nothing in 30 days", model.NeverSentence);
        Assert.Equal("The slowest: **48 s**, Tue 17 Nov, 09:12, 38 files", model.SlowestLine);
        Assert.Equal(new List<int> { 12, 4, 68 }, model.Counts.Select(piece => piece.Count));
        Assert.Equal("Today, 24 Nov. 3 reviews: 1 blocked, 2 allowed.", model.DayDescription);
        Assert.Equal(3, model.NeverRows.Count);
        Assert.Equal(7, model.BrokenRows.Count);

        Assert.True(view.Chart.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.ArrowLeft);
        Assert.Equal("Monday 23 Nov. 4 reviews: 1 incomplete, 3 allowed.", model.DayDescription);
        Assert.True(view.Chart.KeyboardFocus);
        host.Press(PhysicalKey.Home);
        Assert.Equal(0, model.DayIndex);
        host.Press(PhysicalKey.End);
        Assert.Equal(29, model.DayIndex);

        model.ShowNeverCommand.Execute(null);
        model.ShowBrokenCommand.Execute(null);
        host.Pump();
        Assert.Equal(report.NeverFired.Select(rule => rule.Id), model.NeverRows.Select(row => row.Id));
        Assert.Equal(report.MostBroken.Select(rule => rule.Id), model.BrokenRows.Select(row => row.Id));
        Assert.False(model.HasMoreNever);
        Assert.False(model.HasMoreBroken);

        var rule = Assert.IsType<ListBoxItem>(view.NeverList.ContainerFromIndex(0));
        Assert.True(rule.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.Enter);
        Assert.Equal(Screen.Rules, shell.Model.CurrentScreen);
    }

    [AvaloniaTheory]
    [InlineData(false, "each box is one review")]
    [InlineData(true, "each block is a day's reviews of one kind")]
    public async Task ChartNamesItsScaleAndProductFindingsOpenTheirScreens(bool grouped, string chartLabel)
    {
        using var shell = new ShellHost();
        var report = DashboardPictures.Month();
        var days = report.Days.Select(day => grouped ? day with { Allowed = day.Allowed + 25 } : day).ToList();
        days[^1] = days[^1] with { Allowed = 40 };
        var architecture = new StatsRule("ARCH001", "A use breaks a direction", false, Severity.Block);
        var exemption = new StatsRule("ALLOW001", "An exemption needs a reason", false, Severity.Block);
        shell.Stats.Report = report with
        {
            Days = days,
            MostBroken = [new StatsFindingCount(architecture.Id, 2, architecture), new StatsFindingCount(exemption.Id, 1, exemption)]
        };
        shell.Model.CurrentScreen = Screen.Dashboard;
        var model = shell.Model.Dashboard;
        await model.AgainCommand.ExecutionTask!;
        var view = new DashboardView { DataContext = model };
        using var host = new ViewHost(view, 1352, 900);
        Assert.Contains(chartLabel, AutomationProperties.GetName(view.Chart), StringComparison.Ordinal);
        Assert.All(model.BrokenRows, row => Assert.True(row.Blocks));
        Assert.Equal(new List<string> { architecture.Title!, exemption.Title! }, model.BrokenRows.Select(row => row.Title));

        var row = Assert.IsType<ListBoxItem>(view.BrokenList.ContainerFromIndex(0));
        Assert.True(row.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.Enter);
        Assert.Equal(Screen.Architecture, shell.Model.CurrentScreen);

        model.OpenRule(model.BrokenRows[1]);
        Assert.Equal(Screen.Exemptions, shell.Model.CurrentScreen);
        await Signals.Until(shell.Model.Exemptions, () => shell.Model.Exemptions.ShowsList);
    }

    [AvaloniaTheory]
    [InlineData(false, 0, null)]
    [InlineData(false, 3, null)]
    [InlineData(true, 3, null)]
    [InlineData(false, 0, "Access denied")]
    [InlineData(true, 3, "Read interrupted.")]
    public async Task AnEmptyHistoryCanRunTheChangesAndReadProblemsHaveABanner(bool hasReviews, int unreadable, string? readError)
    {
        using var shell = new ShellHost();
        var report = DashboardPictures.Month();
        shell.Stats.Report = report with { Oldest = hasReviews ? report.Oldest : null, UnreadableLines = unreadable, ReadError = readError };
        shell.Model.CurrentScreen = Screen.Dashboard;
        var model = shell.Model.Dashboard;
        await model.AgainCommand.ExecutionTask!;
        using var host = new ViewHost(new DashboardView { DataContext = model }, 760, 1000);
        Assert.Equal(!hasReviews && readError == null, model.IsEmpty);
        Assert.Equal(hasReviews, model.HasReviews);
        Assert.Equal(unreadable > 0 || readError != null, model.HasNotice);
        if(unreadable > 0)
        {
            Assert.NotNull(host.Find<TextBlock>(text => text.IsEffectivelyVisible &&
                text.Text?.Contains("3 reviews in the history could not be read and are left out.", StringComparison.Ordinal) == true));
        }

        var readAgain = host.Find<Button>(button => button.IsEffectivelyVisible && Equals(button.Content, "Read again"));
        if(readError != null)
        {
            var banner = hasReviews ?
                "The history of reviews could not be read in full (read interrupted), so the numbers below may leave reviews out." :
                "The history of reviews could not be read (access denied).";
            Assert.NotNull(host.Find<TextBlock>(text => text.IsEffectivelyVisible && text.Text?.StartsWith(banner, StringComparison.Ordinal) == true));
            Assert.Equal(hasReviews, model.Notice!.Contains("so the numbers below may leave reviews out", StringComparison.Ordinal));
            Assert.Null(host.Find<TextBlock>(text => text.IsEffectivelyVisible && text.Text == StatsReport.EmptyHeading));

            shell.Stats.Report = report;
            Assert.NotNull(readAgain);
            Assert.True(readAgain.Focus(NavigationMethod.Tab));
            host.Press(PhysicalKey.Enter);
            await model.AgainCommand.ExecutionTask!;
            host.Pump();
            Assert.Same(report, model.Report);
            Assert.False(model.HasNotice);
            Assert.False(readAgain.IsEffectivelyVisible);
            return;
        }

        Assert.Null(readAgain);

        if(hasReviews)
            return;

        Assert.NotNull(host.Find<TextBlock>(text => text.IsEffectivelyVisible && text.Text == StatsReport.EmptyHeading));
        shell.Model.Review.SelectedChoice = shell.Model.Review.Choices[1];
        model.ReviewNowCommand.Execute(null);
        await shell.Model.Review.RunCommand.ExecutionTask!;
        Assert.Equal(Screen.Review, shell.Model.CurrentScreen);
        var scope = Assert.Single(shell.Review.Scopes);
        Assert.Equal(ScopeKind.Push, scope.Kind);
        Assert.Equal(ReviewSource.App, scope.Source);
    }
}
