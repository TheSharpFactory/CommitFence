using CF.Adapters.Git;
using CF.Adapters.Machine;
using CF.Adapters.Storage;
using CF.Adapters.Updates;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CF.Languages.CSharp;

namespace CF.Composition;

/// <summary>
/// The one place where the parts are put together. Both programs call it once, at start.
/// </summary>
public static class CompositionRoot
{
    /// <summary>
    /// Builds the services with every language and adapter in place.
    /// </summary>
    public static CommitFenceServices Create()
    {
        var processRunner = new ProcessRunner();
        var dataFolder = new UserDataFolder();
        var fileSystem = new SystemFileSystem();
        var git = new GitCommandLine(processRunner, dataFolder);
        var productInfo = new ProductInfoService();
        var parts = new CompositionParts(fileSystem, new SystemClock(), processRunner, dataFolder, git, productInfo);
        var languages = new List<ILanguage>
        {
            new CSharpLanguage(processRunner, fileSystem, dataFolder)
        };
        var rules = new RuleService(fileSystem, new FileSettingsLock(dataFolder), languages);
        var review = CreateReviewService(parts, languages, new SettingsFile(fileSystem, new SettingsReader(languages)));
        var setup = new SetupService(new UserGitSettings(processRunner), new UserPath(), git, fileSystem);

        return new CommitFenceServices(parts.ProductInfo, parts.Clock, languages, rules, review, setup, new FolderService(fileSystem, git), new ReleaseUpdater(productInfo.Version), new FileUpdateCheckRecord(dataFolder, UpdateProgramName()));
    }

    /// <summary>
    /// The name of the running program without its extension (commitfence or commitfenceapp), which names its own record
    /// of the last check for a newer release.
    /// </summary>
    private static string UpdateProgramName()
    {
        var name = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
        return string.IsNullOrEmpty(name) ? "commitfence" : name;
    }

    private static ReviewService CreateReviewService(CompositionParts parts, IReadOnlyList<ILanguage> languages, SettingsFile settingsFile)
    {
        var settings = new SettingsInTree(parts.FileSystem, settingsFile, parts.ProductInfo.Version);
        var fileReviewer = new FileReviewer(parts.FileSystem, languages);
        var namedFiles = new NamedFilesReview(parts.Clock, languages, settings, fileReviewer);
        var changeSetReview = CreateChangeSetReview(parts, languages, settingsFile, settings, fileReviewer);
        var tree = new TreeReview(parts.Clock, parts.Git, new FileReviewLock(parts.DataFolder), new FileProgramLock(), new ChangeResolver(parts.Git, languages), changeSetReview);

        return new ReviewService(namedFiles, tree, new PushScopeFinder(parts.Git));
    }

    private static ChangeSetReview CreateChangeSetReview(CompositionParts parts, IReadOnlyList<ILanguage> languages, SettingsFile settingsFile, SettingsInTree settings, FileReviewer fileReviewer)
    {
        var snapshots = new GitSnapshotStore(parts.ProcessRunner, parts.DataFolder);
        var reviewLogs = new ReviewLogFolders(parts.DataFolder);
        var toolchain = new ToolchainStage(languages, reviewLogs);
        var structure = new StructureReview(parts.Git, snapshots, reviewLogs, settingsFile);

        return new ChangeSetReview(languages, snapshots, settings, fileReviewer, toolchain, structure);
    }
}