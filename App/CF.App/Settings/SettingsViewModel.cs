using CF.App.Parts;
using CF.App.Shell;
using CF.App.Updates;
using CF.Composition;
using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Settings;

/// <summary>
/// The Settings screen: this repository (the strictness with its table and its sentence, the languages) and this app (the version, the
/// update status, the app's facts, the release notes). Choosing a strictness is saved at once through the settings service. F5 is
/// "Check now".
/// </summary>
internal sealed partial class SettingsViewModel : ObservableObject, IScreenModel
{
    private static readonly string[] _numberWords = ["no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    private readonly ISettingsService _settings;
    private readonly ILanguageService _languageReports;
    private readonly IMachineSettings _machine;
    private readonly string _folderPath;
    private readonly string _settingsPath;
    private readonly bool _followsSystemMotion;

    // set while the screen puts the file's strictness in the choice, so that the choice is not taken for the person's
    private bool _showingChoice;

    private Task _choosing = Task.CompletedTask;

    // the choices that have been made and not yet saved or refused: the table is drawn once the last of them has been
    private int _choicesWaiting;
    private StrictnessReport? _report;

    public SettingsViewModel(CommitFenceServices services, OpenedFolder folder, UpdatesViewModel updates, string installFolder, bool? followsSystemMotion = null)
    {
        _settings = services.Settings;
        _languageReports = services.LanguageReports;
        _machine = services.Machine;
        RepositoryName = folder.Name;
        _folderPath = folder.FolderPath;
        _settingsPath = folder.SettingsPath;
        _followsSystemMotion = followsSystemMotion ?? OperatingSystem.IsWindows();
        VersionTitle = services.ProductInfo.Name + " " + services.ProductInfo.Version;
        Updates = updates;
        Status = new UpdateStatusViewModel(updates, services.UpdateChecks, services.Clock);
        Notes = new ReleaseNotesViewModel(updates, services.ProductInfo);
        Facts = FactsOf(installFolder);
        Status.PropertyChanged += (_, _) => AgainCommand.NotifyCanExecuteChanged();
        Notes.PropertyChanged += OnNotesChanged;
    }

    /// <summary>
    /// Asked for when "What's new" in the update notice opens this screen: the screen scrolls to the version row and puts the
    /// keyboard on "Update now".
    /// </summary>
    public event Action? VersionRevealRequested;

    /// <summary>
    /// The window opens the settings file, from the banner.
    /// </summary>
    public event Action<string>? OpenFileRequested;

    public UpdatesViewModel Updates { get; }

    public UpdateStatusViewModel Status { get; }

    public ReleaseNotesViewModel Notes { get; }

    public string RepositoryName { get; }

    public string RepositoryNote
    {
        get { return $"{RepositoryName}, kept in commitfence.json, for everyone who pulls it"; }
    }

    public string VersionTitle { get; }

    public IReadOnlyList<FactRowViewModel> Facts { get; }

    /// <summary>
    /// The segments of the strictness choice: the three presets, and "Custom" while the file holds a mapping of its own.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<StrictnessChoiceViewModel> Choices { get; private set; } = [];

    [ObservableProperty]
    public partial StrictnessChoiceViewModel? SelectedChoice { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<StrictnessColumnViewModel> Columns { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<StrictnessRowViewModel> Rows { get; private set; } = [];

    /// <summary>
    /// "With Standard, **41** rules of the file block a push, ...", the numbers marked bold.
    /// </summary>
    [ObservableProperty]
    public partial string Sentence { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBanner))]
    public partial string? BannerText { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<LanguageCardViewModel> LanguageCards { get; private set; } = [];

    [ObservableProperty]
    public partial string LanguagesNote { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the notes of the version are shown whole under the version row.
    /// </summary>
    public bool ShowsNotes
    {
        get { return Notes.IsShown; }
        set { Notes.IsShown = value; }
    }

    public bool HasBanner
    {
        get { return BannerText != null; }
    }

    public bool HasStrictness
    {
        get { return _report != null; }
    }

    /// <summary>
    /// Whether "Custom" is the strictness in force: the file's own mapping, which this screen does not edit.
    /// </summary>
    public bool IsCustom
    {
        get { return _report?.Current.Preset == StrictnessPreset.Custom; }
    }

    /// <summary>
    /// Said under the choice while the file holds a mapping of its own: the presets are off until it is removed there. The words
    /// never change; <see cref="IsCustom"/> decides whether they show.
    /// </summary>
    public string CustomNote { get; } = "This repository has a mapping of its own in commitfence.json. Remove it there to choose a preset.";

    /// <summary>
    /// Whether the "Reduce motion" switch stands (Linux and macOS); Windows says it itself and the row is text.
    /// </summary>
    public bool HasMotionSwitch
    {
        get { return !_followsSystemMotion; }
    }

    /// <summary>
    /// Shows the notes of the version and asks the screen to bring its version row into view.
    /// </summary>
    public void ShowVersionNotes()
    {
        ShowsNotes = true;
        VersionRevealRequested?.Invoke();
    }

    /// <summary>
    /// The screen is shown: the notes are folded and what the repository holds is read again. "What's new" opens the screen first and
    /// then shows the notes.
    /// </summary>
    public async Task ShownAsync()
    {
        ShowsNotes = false;
        await RefreshAsync();
    }

    /// <summary>
    /// Reads the strictness, its table and the counts, and the languages with their files, off the interface's thread.
    /// </summary>
    public async Task RefreshAsync()
    {
        BannerText = null;
        var report = await ReadReportAsync();
        var languages = await Task.Run(() => _languageReports.Show(_folderPath));
        ShowReport(report);
        LanguageCards = languages.Select(language => new LanguageCardViewModel(language, RepositoryName)).ToList();
        LanguagesNote = $"Each language comes as a plug-in. This version has {WordOf(languages.Count)}.";
    }

    /// <summary>
    /// Completes when the last choice of a strictness has been saved (or refused) and shown.
    /// </summary>
    public Task ChoiceSettledAsync()
    {
        return _choosing;
    }

    /// <summary>
    /// "Check now" and F5: asks for the latest release.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAsk))]
    private void Again()
    {
        _ = Status.CheckNowCommand.ExecuteAsync(null);
    }

    private bool CanAsk()
    {
        return Updates.IsIdle;
    }

    [RelayCommand]
    private void OpenSettingsFile()
    {
        OpenFileRequested?.Invoke(_settingsPath);
    }

    partial void OnSelectedChoiceChanged(StrictnessChoiceViewModel? value)
    {
        foreach(var choice in Choices)
        {
            choice.IsCurrent = ReferenceEquals(choice, value);
        }

        if(_showingChoice || value == null)
            return;

        _choicesWaiting++;
        _choosing = ChooseAfterAsync(_choosing, value);
    }

    /// <summary>
    /// The choices are saved one after the other, in the order they were made, each against what the file says once the one before is
    /// in: the last choice is the one left in the file.
    /// </summary>
    private async Task ChooseAfterAsync(Task before, StrictnessChoiceViewModel choice)
    {
        await before;
        try
        {
            await ChooseAsync(choice);
        }
        finally
        {
            _choicesWaiting--;
        }
    }

    /// <summary>
    /// The person chose a strictness: it is saved at once through the settings service, and the table, the sentence and the choice
    /// follow. A refusal shows as a banner and the choice goes back to what the file says. While the file holds a mapping of its own,
    /// a preset is not applied: it would replace that mapping, and the choice goes back.
    /// </summary>
    private async Task ChooseAsync(StrictnessChoiceViewModel choice)
    {
        if(_report == null || choice.Preset == _report.Current.Preset)
            return;

        if(choice.IsCustom || IsCustom)
        {
            ShowReport(_report);
            return;
        }

        BannerText = null;
        try
        {
            await Task.Run(() => _settings.SetStrictness(_folderPath, choice.Preset));
        }
        catch(SettingsException exception)
        {
            BannerText = SettingsFileName.FormatError(_settingsPath, exception);
        }
        catch(RuleChangeException exception)
        {
            BannerText = exception.Message;
        }

        var saved = await ReadReportAsync();
        if(_choicesWaiting > 1)
            _report = saved;
        else
            ShowReport(saved);
    }

    /// <summary>
    /// What the file says about strictness, read off the interface's thread. A file that cannot be read shows as the banner and the
    /// table is left empty.
    /// </summary>
    private async Task<StrictnessReport?> ReadReportAsync()
    {
        var (report, error) = await Task.Run(() =>
        {
            try
            {
                return (_settings.Show(_folderPath), (string?)null);
            }
            catch(SettingsException exception)
            {
                return ((StrictnessReport?)null, SettingsFileName.FormatError(_settingsPath, exception));
            }
        });
        if(error != null)
            BannerText = error;

        return report;
    }

    private void ShowReport(StrictnessReport? report)
    {
        _report = report;
        _showingChoice = true;
        try
        {
            ShowTable(report);
        }
        finally
        {
            _showingChoice = false;
        }

        OnPropertyChanged(nameof(HasStrictness));
        OnPropertyChanged(nameof(IsCustom));
    }

    private void ShowTable(StrictnessReport? report)
    {
        if(report == null)
        {
            Choices = [];
            SelectedChoice = null;
            Columns = [];
            Rows = [];
            Sentence = string.Empty;
            return;
        }

        var presets = new List<StrictnessPreset> { StrictnessPreset.Quiet, StrictnessPreset.Standard, StrictnessPreset.Strict };
        if(report.Current.Preset == StrictnessPreset.Custom)
            presets.Add(StrictnessPreset.Custom);

        var custom = report.Current.Preset == StrictnessPreset.Custom;
        Choices = presets.Select(preset => new StrictnessChoiceViewModel(preset, preset == StrictnessPreset.Custom || !custom)).ToList();
        SelectedChoice = Choices.Single(choice => choice.Preset == report.Current.Preset);
        Columns = report.Columns.Select(preset => new StrictnessColumnViewModel(preset.ToString(), preset == report.Current.Preset)).ToList();
        Rows = report.Rows.Select(row => new StrictnessRowViewModel(row.Word, row.Effects.Select((effect, column) => new StrictnessCellViewModel(effect, report.Columns[column] == report.Current.Preset)).ToList())).ToList();
        Sentence = $"With {report.Current.Preset}, **{report.Blocking}** rules of the file block a push, **{report.Warning}** only warn and **{report.Off}** are off. A rule whose effect was changed on Rules keeps it, and team rules keep the effect they were given. Saved at once.";
    }

    /// <summary>
    /// The app's facts: where it runs from, how it looks for updates, how it moves, where it keeps its data.
    /// </summary>
    private List<FactRowViewModel> FactsOf(string installFolder)
    {
        return
        [
            new FactRowViewModel("Folder", installFolder, true),
            new FactRowViewModel("Updates", "Looks for a new version once a day while the app runs. A hook never does.", false),
            MotionRow(),
            new FactRowViewModel("Data", _machine.DataFolder, true, "The history of reviews and the private copies of repositories.")
        ];
    }

    private FactRowViewModel MotionRow()
    {
        if(!_followsSystemMotion)
            return new FactRowViewModel("Motion", "Reduce motion", _machine.ReduceMotion, SaveReduceMotion);

        var reduced = Motion.IsReduced;
        var state = reduced ? "off" : "on";
        var change = reduced ? "on" : "off";
        return new FactRowViewModel("Motion", $"Follows Windows: animations are {state}.", false, $"Turn them {change} in Windows Settings, Accessibility, Visual effects.");
    }

    /// <summary>
    /// "Reduce motion" is saved at once on this machine, and the app moves less from now on.
    /// </summary>
    private void SaveReduceMotion(bool reduce)
    {
        Motion.ReducedOnThisMachine = reduce;
        _machine.SaveReduceMotion(reduce);
    }

    private void OnNotesChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(e.PropertyName == nameof(ReleaseNotesViewModel.IsShown) || string.IsNullOrEmpty(e.PropertyName))
            OnPropertyChanged(nameof(ShowsNotes));
    }

    private static string WordOf(int count)
    {
        return count < _numberWords.Length ? _numberWords[count] : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
