using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Prompts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// One finding under its file: the header (the effect, the message, the rule and its title, the line), and, when it is open,
/// what the rule is to this push, its sentence, the lines around it and the way to the rule.
/// </summary>
internal sealed partial class FindingViewModel : ReviewListItemViewModel
{
    private readonly FileRulePlace? _place;
    private readonly Action<int>? _openLine;
    private readonly Finding _finding;
    private readonly RuleInfo _rule;
    private readonly string? _content;
    private readonly Action<string> _openRule;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCode))]
    public partial IReadOnlyList<CodeLineViewModel> Lines { get; set; } = new List<CodeLineViewModel>();

    public FindingViewModel(Finding finding, RuleInfo rule, string? content, bool startsOpen, Action<string> openRule, bool isFolderReview, Func<ITextClipboard?> clipboard, Action<int>? openLine = null)
    {
        Copy = new CopyButtonViewModel("Copy as a prompt", () => AgentPrompt.ForFinding(finding, rule.Prompt, content), clipboard);
        IsFolderReview = isFolderReview;
        _finding = finding;
        _openLine = openLine;
        if(finding.Source == FindingSource.EditorConfig && finding.File != null)
            _place = rule.FileRule?.ForPath(finding.File);
        _rule = rule;
        _content = content;
        _openRule = openRule;
        IsOpen = startsOpen;
    }

    /// <summary>
    /// The button that puts the prompt for this finding on the clipboard.
    /// </summary>
    public CopyButtonViewModel Copy { get; }

    public bool IsBlock
    {
        get
        {
            return _finding.Severity == Severity.Block;
        }
    }

    public bool IsWarn
    {
        get
        {
            return !IsBlock;
        }
    }

    public string EffectWord
    {
        get
        {
            if(IsFolderReview)
                return IsBlock ? "Would block" : "Would warn";

            return IsBlock ? "Blocks" : "Warns";
        }
    }

    /// <summary>
    /// A finding of a folder review was already in the code: it says what it would do to a push, and stops none.
    /// </summary>
    public bool IsFolderReview { get; }

    /// <summary>
    /// The column of the effect: 76 wide in a review of a push, 100 in a review of a folder.
    /// </summary>
    public double EffectWidth
    {
        get
        {
            return IsFolderReview ? 100 : 76;
        }
    }

    public string Message
    {
        get
        {
            return _finding.Message;
        }
    }

    public string RuleId
    {
        get
        {
            return _finding.RuleId;
        }
    }

    public string Title
    {
        get
        {
            return _rule.Title;
        }
    }

    /// <summary>
    /// The title is left out when the message is the title.
    /// </summary>
    public bool HasTitle
    {
        get
        {
            return _rule.Title.Length > 0 && !string.Equals(_rule.Title.TrimEnd('.'), _finding.Message.TrimEnd('.'), StringComparison.OrdinalIgnoreCase);
        }
    }

    public int? Line
    {
        get
        {
            return _finding.Line;
        }
    }

    public string LineText
    {
        get
        {
            return _finding.Line == null ? string.Empty : "line " + _finding.Line.Value;
        }
    }

    public bool HasLine
    {
        get
        {
            return _finding.Line != null;
        }
    }

    /// <summary>
    /// Where the rule comes from and what it does to this push.
    /// </summary>
    public string SourceLine
    {
        get
        {
            return SourcePrefix + SourceScope + SourceSuffix;
        }
    }

    public string SourcePrefix
    {
        get { return HasSetting ? _rule.Origin + ", set for " : _rule.Origin; }
    }

    public string SourceScope
    {
        get { return _place != null && _rule.FileRule != null ? _rule.FileRule.Scope(_place) : string.Empty; }
    }

    public bool IsScoped
    {
        get { return _place != null && _place != _rule.FileRule?.Main; }
    }

    public string SourceSuffix
    {
        get
        {
            var line = _place == null ? string.Empty : $" on line {_place.Line}";
            if(IsFolderReview)
                return line + (IsBlock ? ", where it blocks a push." : ", where it only warns.") + " It was already in the code, so it stops no push today.";

            return line + (IsBlock ? ". It blocks this push." : ". It only warns, so it does not stop this push.");
        }
    }

    public IReadOnlyList<SentencePieceViewModel> Sentence
    {
        get
        {
            return _rule.Sentence;
        }
    }

    public bool HasSentence
    {
        get
        {
            return _rule.Sentence.Count > 0;
        }
    }

    public bool HasCode
    {
        get
        {
            return Lines.Count > 0;
        }
    }

    public bool HasSetting
    {
        get
        {
            return _place != null;
        }
    }

    public string OpenLineText
    {
        get
        {
            return $"Open .editorconfig at line {_place?.Line}";
        }
    }

    [RelayCommand]
    private void OpenLine()
    {
        if(_place != null)
            _openLine?.Invoke(_place.Line);
    }

    public bool CanOpenRule
    {
        get
        {
            return _rule.CanOpen;
        }
    }

    public string SpokenName
    {
        get
        {
            var title = HasTitle ? " " + _rule.Title : string.Empty;
            return $"{EffectWord}, {Message}, {RuleId}{title}{(HasLine ? ", " + LineText : string.Empty)}, {(IsOpen ? "open" : "folded")}";
        }
    }

    partial void OnIsOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(SpokenName));
        if(value && Lines.Count == 0 && _content != null && _finding.Line != null)
            Lines = CodeAround.Cut(_content, _finding.Line.Value);
    }

    [RelayCommand]
    private void Toggle()
    {
        IsOpen = !IsOpen;
    }

    [RelayCommand]
    private void OpenRule()
    {
        if(_rule.CanOpen)
            _openRule(_finding.RuleId);
    }
}