using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Rules.Settings;

namespace CF.Core.Review;

/// <summary>
/// Runs the toolchains of the languages on the private copy of a change set, and records what they answered.
/// </summary>
public sealed class ToolchainStage
{
    private readonly IReadOnlyList<ILanguage> _languages;
    private readonly IReviewLogs _reviewLogs;

    /// <summary>
    /// Creates the stage.
    /// </summary>
    public ToolchainStage(IReadOnlyList<ILanguage> languages, IReviewLogs reviewLogs)
    {
        _languages = languages;
        _reviewLogs = reviewLogs;
    }

    /// <summary>
    /// What each language's toolchain has to look at in the change, judged on the private copy of the tree.
    /// </summary>
    internal List<ToolchainWork> FindWork(TreeUnderReview tree)
    {
        var work = new List<ToolchainWork>();
        // the team's own settings file is read by CommitFence, never by a build: a push of it alone starts no build
        var changed = tree.ChangeSet.Files.Where(file => !string.Equals(Path.GetFileName(file.Path), SettingsFileName.Default, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach(var language in _languages)
        {
            var toolchain = language.Toolchain;
            if(toolchain == null)
                continue;

            var files = toolchain.FilesOfConcern(tree.Folder, changed);
            if(files.Count > 0)
                work.Add(new ToolchainWork(language, toolchain, files));
        }

        return work;
    }

    /// <summary>
    /// The changed files that no language owns and no toolchain looks at: nothing reads them.
    /// </summary>
    internal static int CountNotLookedAt(TreeUnderReview tree, IReadOnlyList<ToolchainWork> work)
    {
        var concerns = work.SelectMany(item => item.Files).Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        return tree.ChangeSet.Files.Count(file => file.Language == null && !concerns.Contains(file.Path));
    }

    internal List<ToolchainOutcome> Run(TreeUnderReview tree, IReadOnlyList<ToolchainWork> work, bool needsAllAssemblyNames)
    {
        var outcomes = new List<ToolchainOutcome>();
        if(work.Count == 0)
            return outcomes;

        var logFolder = _reviewLogs.CreateFolder(tree.Repository);
        foreach(var item in work)
        {
            var request = new ToolchainRequest(tree.ChangeSet.Tree, tree.Folder, logFolder, item.Files, tree.ChangeSet.IsWholeReview)
            {
                NeedsAllAssemblyNames = needsAllAssemblyNames
            };
            outcomes.Add(new ToolchainOutcome(item, RunToolchain(item, request)));
        }

        return outcomes;
    }

    /// <summary>
    /// The failure boundary of a language's toolchain, drawn once for every language: a toolchain that throws gives a
    /// result that says the build could not be run, and the other toolchains still run.
    /// </summary>
    private static ToolchainResult RunToolchain(ToolchainWork item, ToolchainRequest request)
    {
        try
        {
            return item.Toolchain.Run(request);
        }
        catch(Exception exception) when(!ReviewBoundary.PassesThrough(exception))
        {
            var label = item.Language.Name + " build";
            var reason = "the build could not be run: " + ReviewBoundary.MessageOf(exception);
            var coverage = new List<Coverage> { new Coverage(label, CoverageStatus.Failed, reason, CoverageKind.Build) };
            var meaning = MeaningAnswers.Missing(item.Language.Id, $"{label}: {reason}");
            return new ToolchainResult(new List<Finding>(), coverage, new List<string>(), meaning, new List<FileReading>());
        }
    }

    /// <summary>
    /// Every way the build compiles a changed file, from all the toolchains.
    /// </summary>
    internal static IReadOnlyList<FileReading> ReadingsOf(IReadOnlyList<ToolchainOutcome> outcomes)
    {
        return outcomes.SelectMany(outcome => outcome.Result.Readings).ToList();
    }

    /// <summary>
    /// Adds what the toolchains found and covered to the run, and returns what the build said about the meaning of the
    /// code.
    /// </summary>
    internal static List<LanguageAnswers> Record(IReadOnlyList<ToolchainOutcome> outcomes, ChangeSet changeSet, ReviewRun run)
    {
        var answers = new List<LanguageAnswers>();
        foreach(var outcome in outcomes)
        {
            foreach(var note in outcome.Result.Notes)
                run.AddNote(note);

            foreach(var entry in outcome.Result.Coverage)
                run.AddCoverage(entry);

            run.AddFindings(ReachFilter.Keep(outcome.Result.Findings, changeSet.Files));
            answers.Add(new LanguageAnswers(outcome.Work.Language, outcome.Result.Meaning));
        }

        return answers;
    }
}
