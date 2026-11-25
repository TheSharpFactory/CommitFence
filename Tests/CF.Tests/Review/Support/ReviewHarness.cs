using CF.Adapters.Machine;
using CF.Cli.Commands;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Core.Services;

namespace CF.Tests.Review.Support;

/// <summary>
/// What a command printed and how it ended.
/// </summary>
internal sealed record CommandOutcome(ExitCode ExitCode, string Output, string Error)
{
    public string Text
    {
        get { return Output + Environment.NewLine + Error; }
    }
}

/// <summary>
/// The review as the command line runs it, put together from the product's own parts over a repository in memory: the
/// real resolver, settings, readers and rules, and the fakes for git, the private copy, the locks and the build. A test
/// changes the parts it needs before it runs a command.
/// </summary>
internal sealed class ReviewHarness : IDisposable
{
    private readonly TestFolder _folder = new TestFolder();
    private ReviewService? _service;

    public ReviewHarness()
    {
        RepositoryFolder = Path.Combine(_folder.Path, "repository");
        Directory.CreateDirectory(RepositoryFolder);
        Fake = new FakeGit(RepositoryFolder);
        Git = Fake;
        Snapshots = new FakeSnapshots(Fake, Path.Combine(_folder.Path, "snapshot"));
        FileSystem = new SystemFileSystem();
        Languages = new List<ILanguage> { new TestLanguage() };
    }

    public string RepositoryFolder { get; }

    /// <summary>
    /// A folder of the test that is not the repository.
    /// </summary>
    public string Scratch
    {
        get { return _folder.Path; }
    }

    public FakeGit Fake { get; }

    public IGit Git { get; set; }

    public ISnapshotStore Snapshots { get; set; }

    public IReviewLock ReviewLock { get; set; } = new NoReviewLock();

    public IFileSystem FileSystem { get; set; }

    public IReadOnlyList<ILanguage> Languages { get; set; }

    public IStructureJudgments? StructureJudgments { get; set; }

    public IReviewHistory? History { get; set; }

    public FixedClock Clock { get; } = new FixedClock(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// The review service, put together from the parts as they are now; the same service from then on.
    /// </summary>
    public ReviewService Service()
    {
        _service ??= CreateService();
        return _service;
    }

    public FakeSnapshots? FakeSnapshotStore
    {
        get { return Snapshots as FakeSnapshots; }
    }

    /// <summary>
    /// Runs the command line with the arguments, reading the text as standard input, from the folder (the repository's
    /// own by default).
    /// </summary>
    public CommandOutcome Run(params string[] args)
    {
        return RunWithInput(args, string.Empty);
    }

    public CommandOutcome RunWithInput(string[] args, string input, string? currentFolder = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var environment = new CommandEnvironment(currentFolder ?? RepositoryFolder, "/program", "/program/commitfence", new StringReader(input), output, error, false);
        var runner = new CommandRunner(new FixedProduct(), null!, Service(), null!, null!, environment);
        var exitCode = runner.Run(args);

        return new CommandOutcome(exitCode, output.ToString(), error.ToString());
    }

    public void Dispose()
    {
        _folder.Dispose();
    }

    private ReviewService CreateService()
    {
        var settingsFile = new SettingsFile(FileSystem, new SettingsReader(Languages));
        var settings = new SettingsInTree(FileSystem, settingsFile, FixedProduct.ProgramVersion);
        var fileReviewer = new FileReviewer(FileSystem, Languages);
        var logs = new LogFoldersOfTest(Path.Combine(_folder.Path, "logs"));
        var changeSetReview = new ChangeSetReview(Languages, Snapshots, settings, fileReviewer, new ToolchainStage(Languages, logs), new StructureReview(Git, Snapshots, logs, settingsFile), Git);
        var tree = new TreeReview(Clock, Git, ReviewLock, new NoProgramLock(), new ChangeResolver(Git, Languages), changeSetReview, StructureJudgments, History);

        return new ReviewService(new NamedFilesReview(Clock, Languages, settings, fileReviewer), tree, new PushScopeFinder(Git));
    }
}
