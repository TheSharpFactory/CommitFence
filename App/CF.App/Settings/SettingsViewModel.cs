using CF.Core.Findings;
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

    private StrictnessChange? _undo;
    private StrictnessReport? _report;
    private StrictnessWrite? _refusedWrite;

    public SettingsViewModel(CommitFenceServices services, OpenedFolder folder, UpdatesViewModel updates, string installFolder, bool? followsSystemMotion = null)
    {
        _settings = services.Settings;
        _languageReports = services.LanguageReports;
        _machine = services.Machine;
        RepositoryName = folder.Name;
        _folderPath = Path.GetDirectoryName(folder.SettingsPath)!;
        _settingsPath = folder.SettingsPath;
        _followsSystemMotion = followsSystemMotion ?? OperatingSystem.IsWindows();
        VersionTitle = services.ProductInfo.Name + " " + services.ProductInfo.Version;
        Updates = updates;
        EffectEditors = new List<EffectEditorViewModel>
        {
            new EffectEditorViewModel("error", ChangeEffect),
            new EffectEditorViewModel("warning", ChangeEffect),
            new EffectEditorViewModel("suggestion", ChangeEffect)
        };
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
    /// The three presets and the choice to edit the mapping.
    /// </summary>
    public IReadOnlyList<StrictnessChoiceViewModel> Choices { get; } =
        Enum.GetValues<StrictnessPreset>().Select(preset => new StrictnessChoiceViewModel(preset)).ToList();

    public IReadOnlyList<EffectEditorViewModel> EffectEditors { get; }

    [ObservableProperty]
    public partial string PresetNote { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWriteFailure))]
    [NotifyPropertyChangedFor(nameof(ShowsUndo))]
    public partial string? WriteFailure { get; private set; }

    [ObservableProperty]
    public partial string? FailureDetail { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUndo))]
    [NotifyPropertyChangedFor(nameof(ShowsUndo))]
    public partial string? UndoText { get; private set; }

    public bool HasWriteFailure { get { return WriteFailure != null; } }
    public bool HasUndo { get { return UndoText != null; } }
    public bool ShowsUndo { get { return HasUndo && !HasWriteFailure; } }

    public event Action<string>? StrictnessAnnounced;

    [ObservableProperty]
    public partial StrictnessChoiceViewModel? SelectedChoice { get; set; }

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

    public bool IsCustom
    {
        get { return _report?.Current.Preset == StrictnessPreset.Custom; }
    }

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
        await _choosing;
        _undo = null;
        _refusedWrite = null;
        UndoText = null;
        WriteFailure = null;
        FailureDetail = null;
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
        if(_showingChoice || value == null)
            return;

        QueueWrite(current => value.Preset == StrictnessPreset.Custom
            ? current with { Preset = StrictnessPreset.Custom }
            : Strictness.For(value.Preset));
    }

    private void ChangeEffect(string word, Severity? effect)
    {
        QueueWrite(current => EditEffect(current, word, effect), word);
    }

    [RelayCommand]
    private void Undo()
    {
        _choosing = UndoAfterAsync(_choosing);
    }

    [RelayCommand]
    private void RetryStrictness()
    {
        if(_refusedWrite is not { } write)
            return;

        _refusedWrite = null;
        _choosing = WriteAfterAsync(_choosing, write);
    }

    private void QueueWrite(Func<Strictness, Strictness> change, string? word = null)
    {
        _choosing = WriteAfterAsync(_choosing, new StrictnessWrite(change, word));
    }

    private async Task UndoAfterAsync(Task previous)
    {
        await previous;
        if(_undo is { } undo)
            await WriteAfterAsync(Task.CompletedTask, new StrictnessWrite(_ => undo.Before, null, undo.Revision));
    }

    private async Task WriteAfterAsync(Task previous, StrictnessWrite write)
    {
        await previous;
        string? failure = null;
        try
        {
            var saved = await Task.Run(() => _settings.ChangeStrictness(_folderPath, write.Change, write.UndoRevision));
            RememberUndo(saved, write.UndoRevision != null);
        }
        catch(SettingsException exception)
        {
            failure = SettingsFileName.FormatError(_settingsPath, exception);
        }
        catch(RuleChangeException exception)
        {
            failure = exception.Message;
        }

        BannerText = null;
        var report = await ReadReportAsync();
        ShowReport(report);
        UndoText = _undo is { } retained && report != null && !IsCustom ? DescribeUndo(retained.Before, report.Current) : null;
        _refusedWrite = failure == null ? null : write;
        FailureDetail = failure == null ? null : DescribeRefusal(report?.Current, write.Word);
        WriteFailure = failure;

        if(failure != null)
        {
            StrictnessAnnounced?.Invoke($"{failure} {FailureDetail}");
            return;
        }

        if(report == null)
        {
            StrictnessAnnounced?.Invoke($"Saved at once. {BannerText}");
            return;
        }

        var announcement = write.Word == null
            ? $"{ChoiceName(report.Current.Preset)}. Saved at once."
            : $"{EffectSentence(write.Word, report.Current.EffectOf(write.Word))}. Saved at once.";
        StrictnessAnnounced?.Invoke(UndoText ?? announcement);
    }

    private sealed record StrictnessWrite(Func<Strictness, Strictness> Change, string? Word, string? UndoRevision = null);

    private void RememberUndo(StrictnessChange saved, bool restored)
    {
        if(restored)
            _undo = null;
        else if(saved.Before.Preset == StrictnessPreset.Custom && saved.After.Preset != StrictnessPreset.Custom)
            _undo = saved;
        else if(_undo is { } undo && undo.Revision == saved.PreviousRevision)
            _undo = undo with { Revision = saved.Revision };
    }

    private static Strictness EditEffect(Strictness current, string word, Severity? effect)
    {
        if(current.Preset != StrictnessPreset.Custom)
            throw new RuleChangeException("Could not write commitfence.json: Your own is no longer chosen.");

        return word switch
        {
            "error" => current with { Error = effect },
            "warning" => current with { Warning = effect },
            "suggestion" => current with { Suggestion = effect },
            _ => throw new ArgumentOutOfRangeException(nameof(word))
        };
    }

    private static string DescribeRefusal(Strictness? current, string? word)
    {
        if(current == null)
            return "Your change was not kept.";

        return word == null
            ? $"Your change was not kept: {ChoiceName(current.Preset)} is still in force."
            : $"Your change was not kept: {EffectSentence(word, current.EffectOf(word), " still")}.";
    }

    private string DescribeUndo(Strictness previous, Strictness current)
    {
        var differences = EffectEditors.Where(editor => previous.EffectOf(editor.Word) != current.EffectOf(editor.Word))
            .Select(editor => EffectSentence(editor.Word, previous.EffectOf(editor.Word), past: true)).ToList();
        var lead = $"{ChoiceName(current.Preset)} replaced your own";
        return differences.Count == 0 ? lead + "." : $"{lead}, in which {string.Join(", ", differences)}.";
    }

    private static string EffectSentence(string word, Severity? effect, string still = "", bool past = false)
    {
        var verb = effect switch
        {
            Severity.Block => past ? "blocked a push" : "blocks a push",
            Severity.Warn => past ? "warned" : "warns",
            _ => past ? "was off" : "is off"
        };
        return word + still + " " + verb;
    }

    public static string ChoiceName(StrictnessPreset preset)
    {
        return preset == StrictnessPreset.Custom ? "Your own" : preset.ToString();
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
        SelectedChoice = report == null ? null : Choices.Single(choice => choice.Preset == report.Current.Preset);
        foreach(var choice in Choices)
            choice.IsCurrent = ReferenceEquals(choice, SelectedChoice);

        if(report == null)
            return;

        var current = report.Current;
        var custom = current.Preset == StrictnessPreset.Custom;
        Rows = report.Rows.Select(row => new StrictnessRowViewModel(row.Word, row.Effects.Take(3).Select((effect, column) =>
            new StrictnessCellViewModel(effect, report.Columns[column] == current.Preset))
            .Append(new StrictnessCellViewModel(current.EffectOf(row.Word), custom, isPreview: !custom, isAlwaysOff: custom && row.Word == "silent, none")).ToList())).ToList();
        foreach(var editor in EffectEditors)
            editor.Cell = new StrictnessCellViewModel(current.EffectOf(editor.Word), custom);

        PresetNote = custom
            ? "Your own: choose what each word does, in its column. Saved at once."
            : $"Your own would start as {ChoiceName(current.Preset)}, shown in grey. Choose it to set each word yourself.";
        var name = custom ? "your own" : ChoiceName(current.Preset);
        var off = custom ? "silent and none mean a rule is never reported, so they stay off in your own too. " : string.Empty;
        Sentence = $"{off}With {name}, **{report.Blocking}** rules of the file block a push, **{report.Warning}** only warn and **{report.Off}** are off. A rule whose effect was changed on Rules keeps it, and team rules keep the effect they were given.";
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
