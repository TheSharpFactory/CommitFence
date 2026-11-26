using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Fixes;
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
    private readonly IEnumerable<Finding> _fileFindings;
    private readonly RuleInfo _rule;
    private readonly string? _content;
    private readonly Action<string> _openRule;
    private readonly Func<Task<FixAnswer>>? _prepare;
    private readonly ReviewFixQueue? _fixes;

    public event Action<FindingViewModel, Fix>? FixFinished;

    public Task? Preparation { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFix), nameof(HasDiff), nameof(HasByteChanges), nameof(HasCode), nameof(OffersFix), nameof(ShowsFixButton), nameof(ShowsNoFix), nameof(ReachText), nameof(ByteChangesText))]
    [NotifyCanExecuteChangedFor(nameof(ApplyFixCommand))]
    public partial Fix? Preview { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsNoFix), nameof(ShowsFixButton), nameof(FixButtonText))]
    public partial bool IsPreparing { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiff))]
    public partial IReadOnlyList<FixDiffLine> Difference { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreparationProblem), nameof(ShowsNoFix))]
    public partial string? PreparationProblem { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFixNotice), nameof(FixNoticeText), nameof(IsExcerptStale), nameof(HasFix), nameof(HasDiff), nameof(HasByteChanges), nameof(HasCode), nameof(OffersFix), nameof(ShowsFixButton), nameof(ShowsNoFix))]
    [NotifyCanExecuteChangedFor(nameof(ApplyFixCommand))]
    public partial FixApplied? ApplyFailure { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFix), nameof(HasDiff), nameof(HasByteChanges), nameof(HasCode), nameof(ShowsNoFix), nameof(OffersFix), nameof(ShowsFixButton), nameof(HasPreparationProblem), nameof(HasFixNotice), nameof(FixNoticeText), nameof(IsExcerptStale))]
    [NotifyCanExecuteChangedFor(nameof(ApplyFixCommand))]
    public partial bool FileChanged { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectWord), nameof(ShowsBlock), nameof(ShowsWarn), nameof(SpokenName), nameof(HasFix), nameof(HasDiff), nameof(HasByteChanges), nameof(HasCode), nameof(OffersFix), nameof(ShowsFixButton), nameof(ShowsNoFix))]
    [NotifyCanExecuteChangedFor(nameof(ApplyFixCommand))]
    public partial bool IsFixed { get; private set; }

    public bool HasFix
    {
        get { return Preview != null && !IsExcerptStale && !IsFixed; }
    }

    public bool HasDiff
    {
        get { return HasFix && Difference.Count > 0; }
    }

    public bool HasByteChanges
    {
        get { return HasFix && ByteChangesText.Length > 0; }
    }

    public bool OffersFix
    {
        get { return HasFix && ApplyFailure == null; }
    }

    public bool ShowsFixButton
    {
        get { return IsPreparing || OffersFix; }
    }

    public string FixButtonText
    {
        get { return IsPreparing ? "Preparing a fix" : "Apply fix"; }
    }

    public bool ShowsNoFix
    {
        get { return !IsPreparing && !HasFix && !HasPreparationProblem && !HasFixNotice && !IsFixed; }
    }

    public bool HasPreparationProblem
    {
        get { return PreparationProblem != null && !FileChanged; }
    }

    public bool IsExcerptStale
    {
        get { return FileChanged || ApplyFailure?.FileChanged == true; }
    }

    public bool HasFixNotice
    {
        get { return FileChanged || ApplyFailure != null; }
    }

    public string FixNoticeText
    {
        get
        {
            if(FileChanged)
                return "A fix changed this file after the review. Review again to prepare a fix for this finding.";

            if(ApplyFailure?.FileChanged == true)
                return "Nothing was written to your file: it changed after the review.";

            var reason = ApplyFailure?.Reason?.Trim().TrimEnd('.');
            return reason == null ? string.Empty : $"Nothing was written to your file: {reason}.";
        }
    }

    public string ReachText
    {
        get { return Preview?.DescribeReach(IsFolderReview) ?? string.Empty; }
    }

    public string ByteChangesText
    {
        get { return Preview?.ByteChanges ?? string.Empty; }
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCode))]
    public partial IReadOnlyList<CodeLineViewModel> Lines { get; set; } = new List<CodeLineViewModel>();

    public FindingViewModel(Finding finding, IEnumerable<Finding> fileFindings, RuleInfo rule, string? content, bool startsOpen, Action<string> openRule, bool isFolderReview, Func<ITextClipboard?> clipboard, Action<int>? openLine = null, ReviewFixQueue? fixes = null, Func<Task<FixAnswer>>? prepare = null)
    {
        Copy = new CopyButtonViewModel("Copy as a prompt", () => AgentPrompt.ForFinding(finding, rule.Prompt, content), clipboard);
        IsFolderReview = isFolderReview;
        _finding = finding;
        _fileFindings = fileFindings;
        _openLine = openLine;
        if(finding.Source == FindingSource.EditorConfig && finding.File != null)
            _place = rule.FileRule?.ForPath(finding.File);
        _rule = rule;
        _content = content;
        _openRule = openRule;
        _fixes = fixes;
        _prepare = prepare;
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

    public bool ShowsBlock
    {
        get { return !IsFixed && IsBlock; }
    }

    public bool ShowsWarn
    {
        get { return !IsFixed && !IsBlock; }
    }

    public string EffectWord
    {
        get { return IsFixed ? "Fixed" : ReviewEffectWord(_finding.Severity, IsFolderReview); }
    }

    public static string ReviewEffectWord(Severity severity, bool isFolderReview)
    {
        if(isFolderReview)
            return severity == Severity.Block ? "Would block" : "Would warn";

        return severity == Severity.Block ? "Blocks" : "Warns";
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
            return Lines.Count > 0 && !HasFix;
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

    public bool HasOpenActions
    {
        get { return HasSetting || CanOpenRule; }
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
            Lines = CodeAround.Cut(_content, _finding.Line.Value, IsBlock);

        if(value && _prepare != null && Preparation == null && !FileChanged && !IsFixed)
            Preparation = PrepareAsync(_prepare);
    }

    private async Task PrepareAsync(Func<Task<FixAnswer>> prepare)
    {
        IsPreparing = true;
        try
        {
            var answer = await prepare();
            if(FileChanged || IsFixed)
                return;

            if(answer.Fix == null)
            {
                if(answer.Reason != null)
                    PreparationProblem = answer.NoFixText;

                return;
            }

            Difference = FixDiff.Read(answer.Fix, _finding, _fileFindings, IsFolderReview);
            Preview = answer.Fix;
        }
        finally
        {
            IsPreparing = false;
        }
    }

    private bool CanApplyFix()
    {
        return OffersFix;
    }

    [RelayCommand(CanExecute = nameof(CanApplyFix))]
    private async Task ApplyFixAsync()
    {
        var preview = Preview;
        if(preview == null || _fixes == null || !OffersFix)
            return;

        var applied = await _fixes.ApplyAsync(preview);
        if(applied.Succeeded)
            FixFinished?.Invoke(this, preview);
        else if(!IsFixed && !FileChanged)
            ApplyFailure = applied;
    }

    public void FileWasFixed(bool ruleFixed)
    {
        IsPreparing = false;
        ApplyFailure = null;
        if(!ruleFixed)
        {
            FileChanged = true;
            return;
        }

        IsOpen = false;
        IsFixed = true;
    }

    [RelayCommand]
    private void Toggle()
    {
        if(!IsFixed)
            IsOpen = !IsOpen;
    }

    [RelayCommand]
    private void OpenRule()
    {
        if(_rule.CanOpen)
            _openRule(_finding.RuleId);
    }
}
