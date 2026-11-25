using CF.App.Architecture;
using CF.App.Dashboard;
using CF.App.Doctor;
using CF.App.Exemptions;
using CF.App.Review;
using CF.App.Rules;
using CF.App.Rules.Page;
using CF.App.Settings;
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
    /// <summary>
    /// The screen the rail has chosen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RailIndex), nameof(ShowsRules), nameof(ShowsReview), nameof(ShowsDashboard), nameof(ShowsExemptions), nameof(ShowsArchitecture), nameof(ShowsSettings), nameof(ShowsDoctor), nameof(CurrentModel))]
    public partial Screen CurrentScreen { get; set; } = Screen.Rules;

    public MainWindowViewModel(CommitFenceServices services, string folder)
    {
        var opened = services.Folders.Open(folder);
        Folder = opened;
        VersionText = services.ProductInfo.Name + " " + services.ProductInfo.Version;
        var catalog = new RuleCatalog(services.Rules.ListCatalogs(null, null));
        Rules = new RulesViewModel(services.Rules, opened, catalog, new SettingsFileWatcher(opened.SettingsPath), new SettingsFileWatcher(Path.Combine(Path.GetDirectoryName(opened.SettingsPath) ?? string.Empty, ".editorconfig")));
        Review = new ReviewViewModel(services.Review, services.Rules, services.Clock, opened, OpenRule);
        Updates = new UpdatesViewModel(services.Updater, services.UpdateChecks, services.Clock, opened.FolderPath);
        Dashboard = new DashboardViewModel(services.Stats, opened, OpenDashboardRule, ReviewFromDashboard);
        Exemptions = new ExemptionsViewModel(services.Exemptions, services.Rules, opened, OpenRule, NarrowRule);
        Architecture = new ArchitectureViewModel(services.Structure, services.Clock, opened, new SettingsFileWatcher(opened.SettingsPath),
            Review.HoldsStructureJudgment, judgment =>
            {
                if(Review.ShowStructureJudgment(judgment))
                    ShowReview();
            });
        Settings = new SettingsViewModel(services, opened, Updates);
        Doctor = new DoctorViewModel(services.Doctor, services.Clock, opened);
        Updates.WhatsNewRequested += ShowVersionNotes;
        Rules.FileRulesArrived += ShowRulesLine;
        Review.OpenDoctorRequested += ShowDoctor;
        ShowRulesLine();
    }

    public OpenedFolder Folder { get; }

    public ReviewViewModel Review { get; }

    public UpdatesViewModel Updates { get; }

    public string VersionText { get; }

    public RulesViewModel Rules { get; }

    public DashboardViewModel Dashboard { get; }

    public ExemptionsViewModel Exemptions { get; }

    public ArchitectureViewModel Architecture { get; }

    public SettingsViewModel Settings { get; }

    public DoctorViewModel Doctor { get; }

    /// <summary>
    /// The place of the screen in the rail's list: the rail is one list of seven items, and a click on it chooses a screen by
    /// name here.
    /// </summary>
    public int RailIndex
    {
        get
        {
            return (int)CurrentScreen;
        }
        set
        {
            if(Enum.IsDefined((Screen)value))
                CurrentScreen = (Screen)value;
        }
    }

    /// <summary>
    /// The screen in view when it is one of the five that answer F5, or null for Rules and Review.
    /// </summary>
    public IScreenModel? CurrentModel
    {
        get
        {
            return CurrentScreen switch
            {
                Screen.Dashboard => Dashboard,
                Screen.Exemptions => Exemptions,
                Screen.Architecture => Architecture,
                Screen.Settings => Settings,
                Screen.Doctor => Doctor,
                Screen.Rules => null,
                Screen.Review => null,
                _ => throw new ArgumentOutOfRangeException(nameof(CurrentScreen))
            };
        }
    }

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
            return CurrentScreen == Screen.Rules;
        }
    }

    public bool ShowsReview
    {
        get
        {
            return CurrentScreen == Screen.Review;
        }
    }

    public bool ShowsDashboard
    {
        get
        {
            return CurrentScreen == Screen.Dashboard;
        }
    }

    public bool ShowsExemptions
    {
        get
        {
            return CurrentScreen == Screen.Exemptions;
        }
    }

    public bool ShowsArchitecture
    {
        get
        {
            return CurrentScreen == Screen.Architecture;
        }
    }

    public bool ShowsSettings
    {
        get
        {
            return CurrentScreen == Screen.Settings;
        }
    }

    public bool ShowsDoctor
    {
        get
        {
            return CurrentScreen == Screen.Doctor;
        }
    }

    partial void OnCurrentScreenChanged(Screen value)
    {
        if(value == Screen.Dashboard)
            Dashboard.AgainCommand.Execute(null);

        if(value == Screen.Architecture)
            Architecture.AgainCommand.Execute(null);

        if(value == Screen.Doctor)
            _ = Doctor.ShownAsync();

        if(value == Screen.Settings)
            _ = Settings.ShownAsync();

        if(value == Screen.Exemptions)
            _ = Exemptions.ShownAsync();
        else
            Exemptions.Left();

        if(value != Screen.Review)
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

    private void ReviewFromDashboard()
    {
        ShowReview();
        Review.SelectedChoice = Review.Choices[0];
        if(Review.RunCommand.CanExecute(null))
            Review.RunCommand.Execute(null);
    }

    private void OpenRule(string ruleId)
    {
        Rules.Reveal(ruleId);
        ShowRules();
    }

    private void OpenDashboardRule(string ruleId)
    {
        if(string.Equals(ruleId, StatsRule.LayerDirectionId, StringComparison.OrdinalIgnoreCase))
            CurrentScreen = Screen.Architecture;
        else if(string.Equals(ruleId, StatsRule.MissingExemptionReasonId, StringComparison.OrdinalIgnoreCase))
            CurrentScreen = Screen.Exemptions;
        else
            OpenRule(ruleId);
    }

    /// <summary>
    /// "Narrow the rule" on Exemptions: the rule opens on Rules with a new condition, and its strip's button comes back here, the
    /// rule still chosen.
    /// </summary>
    private void NarrowRule(string ruleId, string stripText)
    {
        Rules.Narrow(ruleId, new NarrowOrigin(stripText, () => CurrentScreen = Screen.Exemptions));
        ShowRules();
    }

    public void ShowRules()
    {
        CurrentScreen = Screen.Rules;
    }

    public void ShowReview()
    {
        CurrentScreen = Screen.Review;
    }

    public void ShowDoctor()
    {
        CurrentScreen = Screen.Doctor;
    }

    /// <summary>
    /// "What's new" in the update notice: Settings opens at the version row with the notes shown. The notice stays.
    /// </summary>
    private void ShowVersionNotes()
    {
        CurrentScreen = Screen.Settings;
        Settings.ShowVersionNotes();
    }

    public void Dispose()
    {
        Architecture.Dispose();
        Rules.Dispose();
    }
}
