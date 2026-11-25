using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Structure;

namespace CF.Core.Review;

/// <summary>
/// Reviews named files against the rules of a settings file, as they lie in a folder.
/// </summary>
/// <remarks>
/// Nothing is built, so the rules that need meaning, and the structure rules, are reported as not run, and a file that
/// holds conditional sections is read in part, so the review is incomplete.
/// </remarks>
public sealed class NamedFilesReview
{
    private readonly IClock _clock;
    private readonly IReadOnlyList<ILanguage> _languages;
    private readonly SettingsInTree _settings;
    private readonly FileReviewer _fileReviewer;

    /// <summary>
    /// Creates the review.
    /// </summary>
    public NamedFilesReview(IClock clock, IReadOnlyList<ILanguage> languages, SettingsInTree settings, FileReviewer fileReviewer)
    {
        _clock = clock;
        _languages = languages;
        _settings = settings;
        _fileReviewer = fileReviewer;
    }

    /// <summary>
    /// Reviews the files. A file that cannot be read, or does not parse, makes the result incomplete.
    /// </summary>
    /// <exception cref="Rules.Settings.SettingsException">The settings file is missing or wrong.</exception>
    public ReviewResult Review(string settingsPath, IReadOnlyList<FileToReview> files)
    {
        var started = _clock.Now;
        var run = new ReviewRun();
        if(_settings.RefusesOlderProgram(settingsPath, run))
            return run.ToResult(_clock.Now - started);

        var settings = _settings.Read(settingsPath);
        foreach(var file in files)
            ReviewOne(file, settings.ActiveRules, run);

        ReportNotRun(settings, run);
        run.ApplyPolicies(settings.Strictness);

        return run.ToResult(_clock.Now - started);
    }

    private void ReviewOne(FileToReview file, IReadOnlyList<RuleDefinition> rules, ReviewRun run)
    {
        try
        {
            _fileReviewer.Review(file, rules, run, null, new List<FileReading>());
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            run.AddCoverage(file.ReportedPath.Replace('\\', '/'), CoverageStatus.Failed, FailureText.Describe(exception));
        }
    }

    private void ReportNotRun(RuleSettings settings, ReviewRun run)
    {
        MeaningRulePass.ReportNotRun(settings.ActiveRules, _languages, "it needs what the compiler knows about the code, and a review of named files does not build", run);
        if(!settings.Structure.IsEmpty)
            run.AddNotRun(StructureRuleInfo.LayerDirectionId, "the layer directions compare the tree before a push with the tree after it, and a review of named files has neither");

        StructureReview.ReportUnsupported(settings.Structure, run);
        AdviseHowToRunThem(run);
    }

    private static void AdviseHowToRunThem(ReviewRun run)
    {
        var count = run.CountNotRun();
        if(count == 0)
            return;

        var rules = count == 1 ? "1 rule needs" : $"{count} rules need";
        var theyWereNotRun = count == 1 ? "it was not run" : "they were not run";
        run.AddAdvice($"{rules} a build, and a review of named files does not run a build, so {theyWereNotRun}: commitfence review --staged or a push runs {(count == 1 ? "it" : "them")}.");
    }
}
