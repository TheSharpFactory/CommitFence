using System.Globalization;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// One path of the within clause: the token with its pattern, and the picker it opens to type the pattern, with what the pattern
/// names in the repository as it is typed. A path that is new has no pattern until the picker keeps one.
/// </summary>
internal sealed partial class PathPieceViewModel : ObservableObject
{
    private const int _filesListed = 5;

    private readonly RulePageViewModel _page;

    [ObservableProperty]
    public partial string Pattern { get; set; }

    [ObservableProperty]
    public partial bool ShowsOr { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string FieldText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Problem { get; set; }

    [ObservableProperty]
    public partial string MatchesBefore { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MatchesCount { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MatchesAfter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<PathFileLine> FilesShown { get; set; } = new List<PathFileLine>();

    [ObservableProperty]
    public partial string MoreText { get; set; } = string.Empty;

    public PathPieceViewModel(RulePageViewModel page, string pattern)
    {
        _page = page;
        Pattern = pattern;
    }

    public bool IsNew
    {
        get { return Pattern.Length == 0; }
    }

    /// <summary>
    /// Whether the picker is open with a pattern in it that the page does not hold yet: an edit that is made and not kept, even
    /// when the pattern is one the page would refuse.
    /// </summary>
    public bool HasOpenDraft
    {
        get { return IsOpen && FieldText.Trim() != Pattern; }
    }

    /// <summary>
    /// A path being typed that the page would refuse: it cannot be kept, so the rule cannot be saved while it is open.
    /// </summary>
    public bool HasInvalidDraft
    {
        get { return HasOpenDraft && Problem != null; }
    }

    /// <summary>
    /// A pattern written by hand in the settings file that the picker would refuse.
    /// </summary>
    public bool IsInvalid
    {
        get { return Pattern.Length > 0 && PathPattern.Problem(Pattern, _page.RepositoryName) != null; }
    }

    public string InvalidText
    {
        get { return PathPattern.Problem(Pattern, _page.RepositoryName) ?? string.Empty; }
    }

    /// <summary>
    /// The pattern the token stands for: the one being typed while the picker refuses it, so that the refused token shows, names and
    /// explains what is refused, else the page's own.
    /// </summary>
    private string ActivePattern
    {
        get { return HasInvalidDraft ? FieldText.Trim() : Pattern; }
    }

    /// <summary>
    /// The active pattern as the list of values the token's text is drawn from.
    /// </summary>
    public IReadOnlyList<string> PatternValues
    {
        get { return [ActivePattern]; }
    }

    /// <summary>
    /// Whether the token is drawn refused: the pattern is one the picker would refuse, or the one being typed in the open picker is.
    /// </summary>
    public bool ShowsRefused
    {
        get { return IsInvalid || HasInvalidDraft; }
    }

    /// <summary>
    /// The reason the active pattern is refused, for the assistive technology of the token it is about; empty while it is not.
    /// </summary>
    public string InvalidReasonSpoken
    {
        get { return HasInvalidDraft ? Problem ?? string.Empty : IsInvalid ? InvalidText : string.Empty; }
    }

    public string TokenText
    {
        get { return IsNew ? "type a path" : PathFileLine.Breakable(Pattern); }
    }

    public string SpokenName
    {
        get { return IsNew ? "Files: not typed yet. Type a path" : $"Files: {ActivePattern}"; }
    }

    public string RemoveSpokenName
    {
        get { return IsNew ? "Remove the path" : $"Remove the path {Pattern}"; }
    }

    public string FieldSpokenName
    {
        get { return $"Path, from the root of {_page.RepositoryName}"; }
    }

    public string RepositoryName
    {
        get { return _page.RepositoryName; }
    }

    public bool HasProblem
    {
        get { return Problem != null; }
    }

    public bool ShowsMatches
    {
        get { return Problem == null; }
    }

    public bool HasFiles
    {
        get { return FilesShown.Count > 0; }
    }

    public bool HasMore
    {
        get { return MoreText.Length > 0; }
    }

    public bool HasCount
    {
        get { return MatchesCount.Length > 0; }
    }

    /// <summary>
    /// Opens the picker with the field holding the pattern, or the character that was typed on the token.
    /// </summary>
    public void Open(string? typed = null)
    {
        FieldText = typed ?? Pattern;
        Refresh();
        IsOpen = true;
    }

    /// <summary>
    /// Keeps the pattern of the field and closes. An empty field removes the path. A pattern that is refused keeps nothing and
    /// the picker stays open with its reason. Returns whether the picker closed.
    /// </summary>
    public bool Commit()
    {
        var typed = FieldText.Trim();
        if(typed.Length == 0)
        {
            IsOpen = false;
            _page.RemovePath(this);
            return true;
        }

        if(PathPattern.Problem(typed, _page.RepositoryName) != null)
        {
            Refresh();
            return false;
        }

        var changed = typed != Pattern;
        Pattern = typed;
        IsOpen = false;
        if(changed)
            _page.PathsChanged();

        return true;
    }

    /// <summary>
    /// Closes with no change. A path that was just added and has no pattern goes again.
    /// </summary>
    public void Cancel()
    {
        IsOpen = false;
        if(IsNew)
            _page.RemovePath(this);
    }

    public void RefreshWords()
    {
        OnPropertyChanged(nameof(ShowsRefused));
        OnPropertyChanged(nameof(IsInvalid));
        OnPropertyChanged(nameof(InvalidReasonSpoken));
        OnPropertyChanged(nameof(InvalidText));
    }

    partial void OnPatternChanged(string value)
    {
        OnPropertyChanged(nameof(PatternValues));
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(TokenText));
        OnPropertyChanged(nameof(SpokenName));
        OnPropertyChanged(nameof(RemoveSpokenName));
        RefreshWords();
    }

    partial void OnFieldTextChanged(string value)
    {
        Refresh();
        DraftChanged();
    }

    partial void OnIsOpenChanged(bool value)
    {
        DraftChanged();
    }

    private void DraftChanged()
    {
        OnPropertyChanged(nameof(HasOpenDraft));
        OnPropertyChanged(nameof(HasInvalidDraft));
        OnPropertyChanged(nameof(ShowsRefused));
        OnPropertyChanged(nameof(PatternValues));
        OnPropertyChanged(nameof(SpokenName));
        OnPropertyChanged(nameof(InvalidReasonSpoken));
        _page.DraftChanged();
    }

    partial void OnProblemChanged(string? value)
    {
        OnPropertyChanged(nameof(HasInvalidDraft));
        OnPropertyChanged(nameof(ShowsRefused));
        OnPropertyChanged(nameof(PatternValues));
        OnPropertyChanged(nameof(SpokenName));
        OnPropertyChanged(nameof(InvalidReasonSpoken));
        OnPropertyChanged(nameof(HasProblem));
        OnPropertyChanged(nameof(ShowsMatches));
    }

    partial void OnFilesShownChanged(IReadOnlyList<PathFileLine> value)
    {
        OnPropertyChanged(nameof(HasFiles));
    }

    partial void OnMoreTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasMore));
    }

    partial void OnMatchesCountChanged(string value)
    {
        OnPropertyChanged(nameof(HasCount));
    }

    [RelayCommand]
    private void OpenPicker()
    {
        Open();
    }

    [RelayCommand]
    private void Remove()
    {
        _page.RemoveByButton(this);
    }

    private void Refresh()
    {
        var typed = FieldText.Trim();
        Problem = typed.Length == 0 ? null : PathPattern.Problem(typed, _page.RepositoryName);
        if(typed.Length == 0 || Problem != null)
        {
            ShowNothing();
            return;
        }

        var matches = _page.FindPathFiles(typed);
        if(matches == null || matches.Count == 0)
            ShowNoFiles();
        else
            ShowFiles(matches);
    }

    private void ShowNothing()
    {
        MatchesBefore = string.Empty;
        MatchesCount = string.Empty;
        MatchesAfter = string.Empty;
        FilesShown = new List<PathFileLine>();
        MoreText = string.Empty;
    }

    private void ShowNoFiles()
    {
        MatchesBefore = $"Matches no {_page.LanguageText} file in {_page.RepositoryName} now.";
        MatchesCount = string.Empty;
        MatchesAfter = string.Empty;
        FilesShown = new List<PathFileLine>();
        MoreText = string.Empty;
    }

    private void ShowFiles(PathMatches matches)
    {
        MatchesBefore = "Matches ";
        MatchesCount = matches.Count.ToString(CultureInfo.InvariantCulture);
        MatchesAfter = matches.Count == 1 ? $" {_page.LanguageText} file" : $" {_page.LanguageText} files";
        FilesShown = matches.Files.Take(_filesListed).Select(path => new PathFileLine(path)).ToList();
        MoreText = matches.Count > _filesListed ? $"and {(matches.Count - _filesListed).ToString(CultureInfo.InvariantCulture)} more" : string.Empty;
    }
}
