using CF.App.Shell;
using CF.Composition;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Services;

namespace CF.App.Tests.Support;

/// <summary>
/// The main window's model over a repository folder in a temporary place, with every service a double: the rules are the real
/// rule service over the folder's settings file, the review, the updater and the record of the last check are the test's.
/// </summary>
internal sealed class ShellHost : IDisposable
{
    private readonly TestFolder _folder = new TestFolder();

    public ShellHost(string? settings = SettingsText.Four, IReadOnlyList<FileRule>? fileRules = null, string repositoryName = "invoicing", IFixService? fixes = null, string? parentFolder = null, ISetupService? setup = null)
    {
        RepositoryPath = Path.Combine(parentFolder ?? _folder.Path, repositoryName);
        Directory.CreateDirectory(RepositoryPath);
        if(settings != null)
            File.WriteAllText(Path.Combine(RepositoryPath, "commitfence.json"), settings);

        Rules = new RuleServiceDouble(Path.Combine(_folder.Path, "data"));
        if(fileRules != null)
            Rules.FileListings = new List<FileRuleListing> { new FileRuleListing("csharp", ".editorconfig", fileRules, new List<string>()) };

        Review.UseRules(Rules, RepositoryPath);
        Services = new CommitFenceServices(new ProductInfoService(), Clock, new List<ILanguage>(), Rules, Review, setup!, new FixedFolderService(Samples.Folder(RepositoryPath, name: repositoryName)), Updater, Record, Doctor, SettingsService, Structure, Languages, Machine, Exemptions, Stats, fixes);
        Model = new MainWindowViewModel(Services, RepositoryPath, Samples.InstallFolder);
    }

    public CommitFenceServices Services { get; }

    public string RepositoryPath { get; }

    public FakeReviewService Review { get; } = new FakeReviewService { PushScope = _ => PushScopeAnswer.Found(ReviewScope.ForPush("origin", "https://example.com/r.git", PushedRefs.None), "main", "origin") };

    public RuleServiceDouble Rules { get; }

    public FixedClock Clock { get; } = new FixedClock(Samples.Now);

    public FakeUpdater Updater { get; } = new FakeUpdater();

    public MemoryCheckRecord Record { get; } = new MemoryCheckRecord();

    public FakeDoctorService Doctor { get; } = new FakeDoctorService();

    public FakeSettingsService SettingsService { get; } = new FakeSettingsService();

    public FakeLanguageService Languages { get; } = new FakeLanguageService();

    public MemoryMachineSettings Machine { get; } = new MemoryMachineSettings();

    public FakeStructureService Structure { get; } = new FakeStructureService();

    public FakeExemptionService Exemptions { get; } = new FakeExemptionService();

    public FakeStatsService Stats { get; } = new FakeStatsService();

    public MainWindowViewModel Model { get; }

    public void Dispose()
    {
        Model.Dispose();
        _folder.Dispose();
    }

    private sealed class FixedFolderService : IFolderService
    {
        private readonly OpenedFolder _folder;

        public FixedFolderService(OpenedFolder folder)
        {
            _folder = folder;
        }

        public OpenedFolder Open(string folder)
        {
            return _folder;
        }
    }
}
