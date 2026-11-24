using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;

namespace CF.Core.Review;

/// <summary>
/// Reads a file once for each way the build compiles it, because code switched by a condition on a symbol is different
/// code in each, and applies the syntax rules to every way.
/// </summary>
/// <remarks>
/// A finding is reported once: when it does not hold in every way, with the projects and frameworks it holds for.
/// </remarks>
internal static class ReadingRules
{
    /// <summary>
    /// The file read in each different way the builds compile it, or nothing when it does not parse with the symbols of
    /// some build (the failure is then recorded in the run).
    /// </summary>
    /// <remarks>
    /// The file is never read with no symbol here: a branch no build compiles is not code of the team's.
    /// </remarks>
    public static IReadOnlyList<ReadWay>? Read(ILanguage language, CodeFile file, IReadOnlyList<FileReading> readings, ReviewRun run)
    {
        var builds = readings.GroupBy(reading => (reading.Project, reading.TargetFramework)).Select(group => group.First()).ToList();
        var ways = new List<ReadWay>();
        foreach(var group in builds.GroupBy(SymbolKey))
        {
            var artifacts = ReadWithSymbols(language, file, group.First(), run);
            if(artifacts == null)
                return null;

            ways.Add(new ReadWay(group.ToList(), artifacts));
        }

        return ways;
    }

    /// <summary>
    /// The findings of the syntax rules over every way the file was read.
    /// </summary>
    public static IReadOnlyList<Finding> Evaluate(IReadOnlyList<RuleDefinition> rules, ILanguage language, IReadOnlyList<ReadWay> ways)
    {
        var found = ways.Select(way => (Way: way, Findings: SyntaxRules.Evaluate(rules, language, way.Artifacts))).ToList();
        var buildCount = ways.Sum(way => way.Builds.Count);
        if(buildCount == 0)
            return found.SelectMany(item => item.Findings).ToList();

        return Merge(found, buildCount);
    }

    private static string SymbolKey(FileReading reading)
    {
        return string.Join(";", reading.Symbols.OrderBy(symbol => symbol, StringComparer.Ordinal));
    }

    private static IReadOnlyList<Artifact>? ReadWithSymbols(ILanguage language, CodeFile file, FileReading reading, ReviewRun run)
    {
        try
        {
            return language.Reader.Read(file, reading.Symbols);
        }
        catch(CodeReadException exception)
        {
            var place = exception.Line == null ? string.Empty : $" at line {exception.Line}, column {exception.Column}";
            run.AddCoverage(file.Path, CoverageStatus.Failed, $"does not parse as {reading.Project} ({reading.TargetFramework}) compiles it{place}: {exception.Message}");
            return null;
        }
    }

    private static List<Finding> Merge(List<(ReadWay Way, List<Finding> Findings)> found, int buildCount)
    {
        var merged = new List<Finding>();
        var groups = found.SelectMany(item => item.Findings.Select(finding => (item.Way, Finding: finding))).GroupBy(item => (item.Finding.RuleId, item.Finding.Line, item.Finding.Column, item.Finding.Message));
        foreach(var group in groups)
        {
            var holders = group.SelectMany(item => item.Way.Builds).ToList();
            var finding = group.First().Finding;
            merged.Add(holders.Count == buildCount ? finding : finding.HoldingFor(JoinDistinct(holders.Select(holder => holder.Project)), JoinDistinct(holders.Select(holder => holder.TargetFramework))));
        }

        return merged;
    }

    private static string JoinDistinct(IEnumerable<string> values)
    {
        return string.Join(", ", values.Distinct());
    }
}