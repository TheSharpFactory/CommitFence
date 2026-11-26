using System.Globalization;
using CF.App.Parts;
using CF.App.Review;
using CF.Core.Code;
using CF.Core.Services;
using CF.Core.Setup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules;

internal sealed partial class RulesViewModel
{
    private readonly ISetupService? _setup;
    private readonly GroupNoteViewModel _setupNote;
    private bool _startedHere;
    private int _setupVisit;
    private const string _settingsName = "commitfence.json";

    /// <summary>
    /// Raised when a start finds that the settings file was made meanwhile and loads it instead of writing one, so the
    /// view can give the keyboard a place: the start button it was on is gone.
    /// </summary>
    public event Action? SettingsAppeared;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartSettingsCommand))]
    [NotifyPropertyChangedFor(nameof(ShowsNoRuleBlock), nameof(ShowsSetup), nameof(ShowsPendingSetupState))]
    public partial bool NeedsSetup { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartLabel), nameof(StartName), nameof(ShowsStartIcon), nameof(SetupState), nameof(ShowsFailedSetupState))]
    [NotifyCanExecuteChangedFor(nameof(StartSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewRuleFromHeadCommand))]
    public partial bool IsStarting { get; set; }

    partial void OnIsStartingChanged(bool value)
    {
        if(_draft != null)
            _draft.IsSetupWriting = value;

        foreach(var page in _pages.Values)
            page.IsSetupWriting = value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartLabel), nameof(StartName), nameof(ShowsStartIcon), nameof(SetupErrorParts), nameof(SetupState), nameof(ShowsFailedSetupState), nameof(ShowsPendingSetupState))]
    public partial string? SetupError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SetupConfirmation), nameof(SetupWhen), nameof(ShowsSetup), nameof(SetupState), nameof(SetupHeading), nameof(SetupReservedLine), nameof(SettingsLocation))]
    public partial string? SetupWrittenAt { get; private set; }

    public bool ShowsSetup { get { return NeedsSetup || SetupWrittenAt != null; } }

    public string? SetupConfirmation { get { return SetupWrittenAt == null ? null : "Started: commitfence.json is written, with no rules yet."; } }

    public string? SetupWhen { get { return SetupWrittenAt == null ? null : $" {SetupWrittenAt}."; } }

    [ObservableProperty]
    public partial string? SetupErrorDetail { get; private set; }

    public string SetupRepository { get { return _folder.Name; } }

    public string? SetupState { get { return IsStarting ? null : SetupWrittenAt != null ? "Written" : SetupError != null ? "Not written" : "Not written yet"; } }

    public IReadOnlyList<DoctorTextPart>? SetupErrorParts
    {
        get { return SetupError == null ? null : SetupMessage.WithFileName(SetupError, _settingsName); }
    }

    public string SetupHeading { get { return SetupWrittenAt == null ? $"CommitFence is not set up in {_folder.Name}" : $"CommitFence is set up in {_folder.Name}"; } }

    public string SetupReservedLine
    {
        get { return $"The team's rules are kept in commitfence.json at the root of {(SetupWrittenAt == null ? "the repository" : _folder.Name)}."; }
    }

    public string SettingsLocation { get { return $"{(SetupWrittenAt == null ? "new" : "written")}, at the root of {_folder.Name}"; } }

    public string StartLabel { get { return IsStarting ? "Starting" : SetupError != null ? "Try again" : "Start CommitFence here"; } }

    public string StartName { get { return IsStarting ? "Starting, busy" : StartLabel; } }

    public bool ShowsStartIcon { get { return !IsStarting && SetupError == null; } }

    public bool ShowsPendingSetupState { get { return NeedsSetup && SetupError == null; } }

    public bool ShowsFailedSetupState { get { return !IsStarting && SetupError != null; } }

    public IReadOnlyList<FileLineViewModel> StarterLines { get; } = MakeStarterLines();

    public ITextClipboard? Clipboard { get; set; }

    public CopyButtonViewModel CopyStarter { get; }

    [ObservableProperty]
    public partial SetupNextStep? CommitSettings { get; private set; }

    private bool MissingSettings()
    {
        return _setup != null && _folder.InsideRepository && !_setup.HasSettings(_folder.SettingsPath);
    }

    private bool CanStartSettings()
    {
        return NeedsSetup || IsStarting;
    }

    public void Left()
    {
        _setupVisit++;
        SetupWrittenAt = null;
    }

    [RelayCommand(CanExecute = nameof(CanStartSettings), AllowConcurrentExecutions = true)]
    private async Task StartSettingsAsync()
    {
        if(IsStarting || !NeedsSetup)
            return;

        SetupError = null;
        SetupErrorDetail = null;
        if(!MissingSettings())
        {
            Reload();
            SettingsAppeared?.Invoke();
            return;
        }

        var visit = _setupVisit;
        IsStarting = true;
        try
        {
            var result = await Task.Run(() => _setup!.StartSettings(_folder.FolderPath));
            if(result.Failed && MissingSettings())
            {
                SetupErrorDetail = result.Lines.Count > 1 ? string.Join(" ", result.Lines.Skip(1)) : null;
                SetupError = result.Lines[0];
                return;
            }

            _startedHere = result.SettingsCreated;
            Reload();
            if(!result.SettingsCreated && !NeedsSetup)
                SettingsAppeared?.Invoke();
            if(result.SettingsCreated && !NeedsSetup && _definitions.Count == 0 && visit == _setupVisit && _clock != null)
            {
                var at = _clock.ToLocalTime(_clock.Now).ToString("HH:mm", CultureInfo.InvariantCulture);
                CommitSettings = result.WorktreeRoot is { } root
                    ? new SetupNextStep(root, _settingsName, "so the team gets the same rules.", "Add CommitFence settings", () => Clipboard)
                    : null;
                SetupWrittenAt = $"Today, {at}";
            }
        }
        finally
        {
            IsStarting = false;
        }
    }

    private static List<FileLineViewModel> MakeStarterLines()
    {
        var models = new List<FileLineViewModel>();
        foreach(var text in DoctorExcerpt.ReadLines(SetupService.StarterSettings))
        {
            var model = new FileLineViewModel();
            model.Take(new FileLine(models.Count + 1, text), false);
            model.Segments = text.Contains("\"rules\"", StringComparison.Ordinal)
                ? [new CodeSegment("    ", null), new CodeSegment("\"rules\"", "code-string"), new CodeSegment(": []", null)]
                : [new CodeSegment(text, null)];
            models.Add(model);
        }

        return models;
    }
}
