using CF.Core.Findings;
using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>Prints the same local push review history as Dashboard.</summary>
internal sealed class StatsCommand(IStatsService stats, CommandEnvironment environment)
{
    public ExitCode Run(string[] args)
    {
        var showAll = args is [_, "--all"];
        if(args.Length != 1 && !showAll)
        {
            environment.Error.WriteLine(UsageText.Text);
            return ExitCode.BadSettingsOrUsage;
        }

        var report = stats.Read(environment.CurrentFolder);
        if(report == null)
        {
            environment.Error.WriteLine("The current folder is not inside a git repository.");
            return ExitCode.BadSettingsOrUsage;
        }

        var output = environment.Output;
        if(report.ReadErrorNotice is { } readError)
            output.WriteLine(readError);

        if(report.UnreadableNotice is { } notice)
            output.WriteLine(notice);

        if(report.Oldest == null)
        {
            if(report.ReadError == null)
            {
                output.WriteLine(StatsReport.EmptyHeading);
                output.WriteLine(StatsReport.EmptySentence);
            }

            return ExitCode.Passed;
        }

        WriteReviews(output, report);
        WriteRules(output, report, showAll);
        output.WriteLine("Time per review");
        foreach(var band in report.Timing.Bands)
            output.WriteLine($"  {band.Label}: {band.Count}{(band.Note.Length == 0 ? string.Empty : " - " + band.Note)}");

        return ExitCode.Passed;
    }

    private static void WriteReviews(TextWriter output, StatsReport report)
    {
        output.WriteLine(report.Period);
        output.WriteLine($"{report.Totals.ReviewsText} in 30 days");
        if(report.Totals.Total > 0)
            output.WriteLine(report.Totals.CountsText());

        var times = new List<string>();
        if(report.Timing.TypicalMilliseconds is { } typical)
            times.Add(StatsTiming.Seconds(typical) + " typical");

        if(report.Timing.Slowest is { } slowest)
            times.Add(StatsTiming.Seconds(slowest.Milliseconds) + " slowest");

        if(times.Count > 0)
            output.WriteLine(string.Join(", ", times));

        foreach(var day in report.Days.Where(day => day.Total > 0))
            output.WriteLine($"  {day.Label(day.Date == report.Days[^1].Date)}: {day.ReviewsText}, {day.CountsText()}");
    }

    private static void WriteRules(TextWriter output, StatsReport report, bool showAll)
    {
        output.WriteLine("Most broken");
        output.WriteLine(report.MostBroken.Count == 0 ? "No findings in 30 days." :
            $"{report.MostBroken.Count} {(report.MostBroken.Count == 1 ? "rule had" : "rules had")} findings");
        foreach(var row in report.MostBroken)
            output.WriteLine($"  {Describe(row.Id, row.Rule)}: {row.Count}");

        output.WriteLine("Rules that never fired");
        output.WriteLine(report.NeverFiredSentence);
        var shown = showAll ? report.NeverFired.Count : 3;
        foreach(var rule in report.NeverFired.Take(shown))
            output.WriteLine("  " + Describe(rule.Id, rule));

        if(report.NeverFired.Count > shown)
            output.WriteLine($"  and {report.NeverFired.Count - shown} more: commitfence stats --all");
    }

    private static string Describe(string id, StatsRule? rule)
    {
        var title = rule?.Title is { Length: > 0 } name ? id + " " + name : id;
        return rule?.Effect switch
        {
            Severity.Block => title + " (Blocks)",
            Severity.Warn => title + " (Warns)",
            _ => title
        };
    }
}
