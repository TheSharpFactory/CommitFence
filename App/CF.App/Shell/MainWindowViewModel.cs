using CF.App.Review;
using CF.App.Rules;
using CF.App.Rules.Page;
using CF.App.Updates;
using CF.Composition;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Shell;

/// <summary>
/// The window: which screen the rail shows, which repository the program works on, and the screens themselves.
/// </summary>
internal sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const int _rulesIndex = 0;
    private const int _reviewIndex = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRules), nameof(ShowsReview))]
    public partial int ScreenIndex { get; set; } = _rulesIndex;

    public MainWindowViewModel(CommitFenceServices services, string folder)
    {
        var opened = services.Folders.Open(folder);
        Folder = opened;
        VersionText = services.ProductInfo.Name + " " + services.ProductInfo.Version;
        var catalog = new RuleCatalog(services.Rules.ListCatalogs(null, null));
        Rules = new RulesViewModel(services.Rules, opened, catalog, new SettingsFileWatcher(opened.SettingsPath), new SettingsFileWatcher(Path.Combine(Path.GetDirectoryName(opened.SettingsPath) ?? string.Empty, ".editorconfig")));
        Review = new ReviewViewModel(services.Review, services.Rules, services.Clock, opened, OpenRule);
        Updates = new UpdatesViewModel(services.Updater, services.UpdateChecks, services.Clock, opened.FolderPath);
        Rules.FileRulesArrived += ShowRulesLine;
        ShowRulesLine();
    }

    public OpenedFolder Folder { get; }

    public ReviewViewModel Review { get; }

    public UpdatesViewModel Updates { get; }

    public string VersionText { get; }

    public RulesViewModel Rules { get; }

    public string RepositoryName
    {
        get
        {
            return Folder.Name;
        }
    }

    public string BranchText
    {
        get
        {
            return Folder.Branch ?? string.Empty;
        }
    }

    public bool HasBranch
    {
        get
        {
            return Folder.Branch != null;
        }
    }

    /// <summary>
    /// What the repository button says in a tooltip and to a screen reader.
    /// </summary>
    public string RepositorySpokenName
    {
        get
        {
            if(Folder.InsideRepository)
                return $"Repository: {Folder.Name}{(Folder.Branch == null ? string.Empty : ", branch " + Folder.Branch)}";

            return $"Folder: {Folder.Name}, not inside a git repository";
        }
    }

    public bool ShowsRules
    {
        get
        {
            return ScreenIndex == _rulesIndex;
        }
    }

    public bool ShowsReview
    {
        get
        {
            return ScreenIndex == _reviewIndex;
        }
    }

    partial void OnScreenIndexChanged(int value)
    {
        if(value != _reviewIndex)
            return;

        ShowRulesLine();
        _ = Review.RefreshAsync();
    }

    /// <summary>
    /// Tells the Review screen how many rules are on, as the filter of Rules counts them, once both groups are read.
    /// </summary>
    private void ShowRulesLine()
    {
        Review.ShowRulesLine(Rules.RuleCountsAreKnown ? new RulesOn(Rules.CountOf(RuleFilter.Blocks), Rules.CountOf(RuleFilter.Warns)) : null);
    }

    private void OpenRule(string ruleId)
    {
        Rules.Reveal(ruleId);
        ShowRules();
    }

    public void ShowRules()
    {
        ScreenIndex = _rulesIndex;
    }

    public void ShowReview()
    {
        ScreenIndex = _reviewIndex;
    }

    public void Dispose()
    {
        Rules.Dispose();
    }
}
