using Avalonia.Input;
using Avalonia.Styling;
using CF.App.Dashboard;
using CF.App.Shell;
using CF.Core.Findings;
using CF.Core.Services;

namespace CF.App.Tests.Support;

/// <summary>The Dashboard at both widths, with a month's history and before its first review.</summary>
internal static class DashboardPictures
{
    private static readonly DateOnly _start = new DateOnly(2026, 10, 26);

    public static async Task SaveAsync(string folder, string theme)
    {
        using var shell = new ShellHost(repositoryName: "CommitFence");
        shell.Stats.Report = Month();
        shell.Model.CurrentScreen = Screen.Dashboard;
        await shell.Model.Dashboard.AgainCommand.ExecutionTask!;
        var window = new MainWindow
        {
            DataContext = shell.Model,
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = theme == "light" ? ThemeVariant.Light : ThemeVariant.Dark
        };
        using var host = new ViewHost(window);
        host.Save(folder, $"dashboard-{theme}-1440.png");
        if(theme == "light")
        {
            window.Height = 1200;
            host.Save(folder, "dashboard-full-1440.png");
            window.Height = 900;
            var chart = host.Find<DashboardChart>(control => control.IsEffectivelyVisible)!;
            chart.Focus(NavigationMethod.Tab);
            for(var i = 0; i < 7; i++)
                host.Press(PhysicalKey.ArrowLeft);

            host.Save(folder, "dashboard-day-1440.png");
            host.Press(PhysicalKey.End);
            window.Focusable = true;
            window.Focus(NavigationMethod.Pointer);
            window.Width = 760;
            window.Height = 1000;
            host.Save(folder, "dashboard-light-760.png");
            window.Height = 1800;
            host.Save(folder, "dashboard-full-760.png");

            var days = shell.Stats.Report.Days.ToList();
            var lastDay = days[^1];
            days[^1] = lastDay with { Refused = 9, Incomplete = 2, Allowed = 30 };
            var timing = shell.Stats.Report.Timing;
            var bands = timing.Bands.ToList();
            bands[1] = bands[1] with { Count = bands[1].Count + days[^1].Total - lastDay.Total };
            shell.Stats.Report = shell.Stats.Report with { Days = days, Timing = timing with { Bands = bands }, ReadError = "The read was interrupted" };
            await shell.Model.Dashboard.AgainCommand.ExecuteAsync(null);
            window.Width = 1440;
            window.Height = 900;
            host.Save(folder, "dashboard-busy-day-read-error-1440.png");
            shell.Stats.Report = Month();
            await shell.Model.Dashboard.AgainCommand.ExecuteAsync(null);
            shell.Model.Dashboard.ShowBrokenCommand.Execute(null);
            host.Pump();
            var dashboard = host.Find<DashboardView>(view => view.IsEffectivelyVisible)!;
            dashboard.BrokenList.ScrollIntoView(shell.Model.Dashboard.BrokenRows.Count - 1);
            host.Save(folder, "dashboard-all-broken-1440.png");
        }

        shell.Stats.Report = shell.Stats.Report with { Oldest = null };
        await shell.Model.Dashboard.AgainCommand.ExecuteAsync(null);
        window.Width = 1440;
        window.Height = 900;
        var suffix = theme == "light" ? string.Empty : "-dark";
        host.Save(folder, $"dashboard-empty{suffix}-1440.png");
    }

    public static StatsReport Month()
    {
        var counts = new List<(int Allowed, int Refused, int Incomplete)>
        {
            (3, 1, 0), (4, 0, 0), (2, 1, 0), (3, 0, 1), (2, 0, 0), (0, 0, 0), (1, 0, 0),
            (3, 1, 0), (4, 2, 0), (3, 0, 0), (2, 0, 0), (3, 1, 0), (0, 0, 0), (0, 0, 0),
            (4, 0, 0), (3, 0, 1), (4, 1, 0), (3, 0, 0), (2, 0, 0), (0, 0, 0), (0, 0, 0),
            (3, 1, 0), (4, 2, 1), (3, 0, 0), (4, 1, 0), (2, 0, 0), (0, 0, 0), (1, 0, 0),
            (3, 0, 1), (2, 1, 0)
        };
        var days = counts.Select((row, i) => new StatsDay(_start.AddDays(i), row.Refused, row.Incomplete, row.Allowed)).ToList();
        var timing = new StatsTiming(6400, new StatsReviewTime(new DateTimeOffset(2026, 11, 17, 9, 12, 0, TimeSpan.Zero), 48000, 38),
        [
            new StatsTimeBand("Under 5 s", 15, "5 of them quick reviews, which build nothing"),
            new StatsTimeBand("5 to 10 s", 65, "Typical: 6.4 s"),
            new StatsTimeBand("10 to 20 s", 2, "12 s and 18 s"),
            new StatsTimeBand("Over 20 s", 2, "27 s, and the slowest: 48 s, Tue 17 Nov, 09:12, 38 files")
        ]);
        var most = new List<StatsFindingCount>
        {
            Broken("IDE0055", "Fix formatting", 31, Severity.Block),
            Broken("TEAM001", "No braces around a single-line if", 17, Severity.Block),
            Broken("CA1822", "Mark members as static", 12, Severity.Warn),
            Broken("IDE0005", "Remove unnecessary usings", 9, null),
            Broken("TEAM011", "Only public and private methods inside an internal class", 6, Severity.Block),
            Broken("IDE0051", "Remove unused private members", 4, Severity.Warn),
            Broken("CA1068", "CancellationToken parameters must come last", 3, Severity.Warn),
            Broken("TEAM012", "Use IClock, not DateTime.Now", 2, Severity.Block),
            Broken("CA1000", "Do not declare static members on generic types", 1, Severity.Block),
            Broken("CA1024", "Use properties where appropriate", 1, Severity.Warn),
            Broken("CA1816", "Call GC.SuppressFinalize correctly", 1, Severity.Warn),
            new StatsFindingCount("OLD001", 1, null)
        };
        var never = new List<StatsRule>
        {
            new StatsRule("TEAM013", "Core uses no file, process or network API", false, Severity.Block),
            new StatsRule("CA1010", "Collections should implement generic interface", true, Severity.Warn),
            new StatsRule("CA1050", "Declare types in namespaces", true, Severity.Block)
        };
        never.AddRange(Enumerable.Range(0, 29).Select(number => new StatsRule($"IDE{1000 + number}", "Check this project's code style", true, Severity.Warn)));
        return new StatsReport(days, _start, 0, timing, most, never, 47);
    }

    private static StatsFindingCount Broken(string id, string title, long count, Severity? effect)
    {
        return new StatsFindingCount(id, count, new StatsRule(id, title, !id.StartsWith("TEAM", StringComparison.Ordinal), effect));
    }
}
