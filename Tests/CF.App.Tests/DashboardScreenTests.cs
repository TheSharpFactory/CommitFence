using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CF.App.Dashboard;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Services;
using Xunit;

namespace CF.App.Tests;

/// <summary>The Dashboard presents history and uses the ordinary Review and Rules entry points.</summary>
public sealed class DashboardScreenTests
{
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
        Assert.Equal("84 in 30 days, by the hook and on Review", model.ReviewNote);
        Assert.Equal(new List<int> { 12, 4, 68 }, model.Counts.Select(piece => piece.Count));
        Assert.Equal("Today, 24 Nov. 3 reviews: 1 refused a push, 2 allowed.", model.DayDescription);
        Assert.Equal(3, model.NeverRows.Count);
        Assert.Equal(8, model.BrokenRows.Count);

        Assert.True(view.Chart.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.ArrowLeft);
        Assert.Equal("Monday 23 Nov. 4 reviews: 1 incomplete, 3 allowed.", model.DayDescription);
        Assert.True(view.ChartRing.IsVisible);
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
    [InlineData(40, "Each box is one review")]
    [InlineData(41, "Each block is a day's reviews of one kind")]
    public async Task ChartNamesItsScaleAndProductFindingsOpenTheirScreens(int total, string chartLabel)
    {
        using var shell = new ShellHost();
        var report = DashboardPictures.Month();
        var days = report.Days.ToList();
        days[^1] = days[^1] with { Allowed = total - 1 };
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

        if(readError != null)
        {
            Assert.NotNull(host.Find<TextBlock>(text => text.IsEffectivelyVisible &&
                text.Text?.StartsWith($"The history of reviews could not be read: {readError.TrimEnd('.')}.", StringComparison.Ordinal) == true));
            Assert.Null(host.Find<TextBlock>(text => text.IsEffectivelyVisible && text.Text == StatsReport.EmptyHeading));
            return;
        }

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
