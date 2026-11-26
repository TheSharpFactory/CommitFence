using System.Text.RegularExpressions;
using CF.App.Review;
using CF.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// Try it on the page of a rule: the code in the box, and what the rule says about it. The code is kept while the app runs
/// and never written to disk. The rule is run off the window's thread, so a key press never waits for it, and when the code or
/// the sentence changes while a run is going the answer to the older text is dropped and the newest text is run.
/// </summary>
internal sealed partial class TryItViewModel : ObservableObject
{
    private const string _unfinished = "Finish the rule to try it.";
    private const string _noArtifactYet = "the artifact";

    private TryPlan? _plan;
    private IReadOnlyList<string> _propertyNames = new List<string>();
    private string _artifactWords = _noArtifactYet;
    private int _wanted;

    /// <summary>
    /// The text of the box. Setting it starts a run of the rule on it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCode), nameof(Placeholder), nameof(ShowsEmptyBlock), nameof(ShowsHint))]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsCounts), nameof(ShowsParse), nameof(ShowsMessage))]
    public partial TryPhase Phase { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ParseText { get; set; } = string.Empty;

    /// <summary>
    /// What Try it says under its box when the rule applies only to some paths; empty when it applies to every file.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPathsNote))]
    public partial string PathsNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Breaks { get; set; }

    [ObservableProperty]
    public partial int Follows { get; set; }

    [ObservableProperty]
    public partial int NotCovered { get; set; }

    /// <summary>
    /// The verdicts shown in the code: one for each artifact, in the order the artifacts start.
    /// </summary>
    public IReadOnlyList<ArtifactVerdict> Verdicts { get; private set; } = new List<ArtifactVerdict>();

    private string _ruleId = string.Empty;
    private string _repositoryName = string.Empty;

    public TryItViewModel()
    {
        Copy = new CopyButtonViewModel("Copy", () => Command, () => Clipboard);
        Copy.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CopySpokenName));
    }

    /// <summary>
    /// Raised on the window's thread each time what Try it shows has changed.
    /// </summary>
    public event Action? Shown;

    /// <summary>
    /// Whether the rule reads what pasted code does not have, the name of the file: it is tried on a file, from a terminal, and the
    /// page shows the command in place of the code box.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsCodeBox), nameof(ShowsCommand), nameof(CommandSentence))]
    public partial bool ReadsFileName { get; set; }

    /// <summary>
    /// Whether the rule is saved as the page shows it. The command runs the saved rule, so a new rule or one with unsaved changes
    /// cannot be tried on a file yet.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsCommand), nameof(CommandSentence))]
    public partial bool IsSaved { get; set; }

    /// <summary>
    /// Whether the command line with its Copy button is shown: the rule is tried on a file and is saved.
    /// </summary>
    public bool ShowsCommand
    {
        get { return ReadsFileName && IsSaved; }
    }

    public bool ShowsCodeBox
    {
        get { return !ReadsFileName; }
    }

    public string Command
    {
        get { return $"commitfence rules try {_ruleId} <file>"; }
    }

    public string CopySpokenName
    {
        get { return Copy.IsCopied ? "Copied" : $"Copy the command {Command}"; }
    }

    public CopyButtonViewModel Copy { get; }

    /// <summary>
    /// Where the Copy button puts the command; the window gives it.
    /// </summary>
    public ITextClipboard? Clipboard { get; set; }

    /// <summary>
    /// The id of the rule, which the command names.
    /// </summary>
    public void RuleChanged(string ruleId, string repositoryName)
    {
        if(ruleId == _ruleId && repositoryName == _repositoryName)
            return;

        _ruleId = ruleId;
        _repositoryName = repositoryName;
        OnPropertyChanged(nameof(Command));
        OnPropertyChanged(nameof(CommandSentence));
        OnPropertyChanged(nameof(CopySpokenName));
    }

    public string CommandSentence
    {
        get
        {
            return IsSaved
                ? $"This rule reads the name of the file, which pasted code does not have, so it is tried on a file instead. Run this in a terminal in `{_repositoryName}`, with the path of a file:"
                : "Save the rule to try it on a file.";
        }
    }

    public bool HasPathsNote
    {
        get
        {
            return PathsNote.Length > 0;
        }
    }

    /// <summary>
    /// The paths the rule applies to, as the rule writes them: the sample is judged as if it were a file of those paths.
    /// </summary>
    public void PathsChanged(IReadOnlyList<string> paths)
    {
        PathsNote = paths.Count == 0 ? string.Empty : $"This rule applies only to {string.Join(", ", paths)}. The sample is judged as if it were one of those files.";
    }

    public bool HasCode
    {
        get
        {
            return !string.IsNullOrWhiteSpace(Code);
        }
    }

    /// <summary>
    /// The box holds nothing and the rule can be tried: the body of the box shows the block that says what to put in it.
    /// </summary>
    public bool ShowsEmptyBlock
    {
        get
        {
            return !HasCode && _plan != null && Example != null;
        }
    }

    /// <summary>
    /// The box holds nothing and the rule cannot be tried here: line 1 holds the placeholder, as a hint.
    /// </summary>
    public bool ShowsHint
    {
        get
        {
            return !HasCode && !ShowsEmptyBlock;
        }
    }

    public string? Example
    {
        get
        {
            return TryExamples.For(_artifactWords);
        }
    }

    public bool HasExample
    {
        get
        {
            return Example != null;
        }
    }

    /// <summary>
    /// "Paste an if statement, or the method around it."
    /// </summary>
    public string BlockSentence
    {
        get
        {
            return $"Paste {Article(_artifactWords)} {_artifactWords}, or the {Around(_artifactWords)} around it.";
        }
    }

    /// <summary>
    /// "Each if statement gets one of these labels."
    /// </summary>
    public string BlockLegendLine
    {
        get
        {
            return $"Each {_artifactWords} gets one of these labels.";
        }
    }

    /// <summary>
    /// Raised when "Load an example" has put the example in the box: the view moves the keyboard to the box, caret at the start.
    /// </summary>
    public event Action? ExampleLoaded;

    [RelayCommand]
    private void LoadExample()
    {
        var example = Example;
        if(example == null)
            return;

        Code = example;
        ExampleLoaded?.Invoke();
    }

    /// <summary>
    /// Whether a run of the rule is going: Try it does not show the answer to the newest text yet.
    /// </summary>
    public bool IsBusy { get; private set; }

    public bool ShowsCounts
    {
        get
        {
            return Phase == TryPhase.Tried;
        }
    }

    public bool ShowsParse
    {
        get
        {
            return Phase == TryPhase.DoesNotParse;
        }
    }

    public bool ShowsMessage
    {
        get
        {
            return Phase is TryPhase.Unfinished or TryPhase.Refused or TryPhase.NeedsBuild or TryPhase.NoArtifact;
        }
    }

    public bool NoBreaks
    {
        get
        {
            return Breaks == 0;
        }
    }

    public bool NoFollows
    {
        get
        {
            return Follows == 0;
        }
    }

    public bool NoNotCovered
    {
        get
        {
            return NotCovered == 0;
        }
    }

    public string CountsSpokenName
    {
        get
        {
            return $"{BreaksWord}, {FollowsWord}, {NotCoveredWord}";
        }
    }

    public string BreaksWord
    {
        get
        {
            return $"{Breaks} {(Breaks == 1 ? "breaks" : "break")} it";
        }
    }

    public string FollowsWord
    {
        get
        {
            return $"{Follows} {(Follows == 1 ? "follows" : "follow")} it";
        }
    }

    public string NotCoveredWord
    {
        get
        {
            return $"{NotCovered} not covered";
        }
    }

    /// <summary>
    /// What stands in the box, in grey, while it is empty: it names the artifact the rule is for.
    /// </summary>
    public string Placeholder
    {
        get
        {
            return PlaceholderFor(_artifactWords);
        }
    }

    /// <summary>
    /// The sentence changed: what the rule is for, and the rule ready to run, or nothing when the sentence is not complete.
    /// A sentence that has not changed since the last call starts nothing.
    /// </summary>
    public void SentenceChanged(string artifactWords, IReadOnlyList<string> propertyNames, TryPlan? plan)
    {
        _propertyNames = propertyNames;
        if(artifactWords != _artifactWords)
        {
            _artifactWords = artifactWords;
            OnPropertyChanged(nameof(Placeholder));
            OnPropertyChanged(nameof(BlockSentence));
            OnPropertyChanged(nameof(BlockLegendLine));
            OnPropertyChanged(nameof(HasExample));
        }

        if(Equals(plan?.Key, _plan?.Key) && (plan == null) == (_plan == null))
            return;

        _plan = plan;
        OnPropertyChanged(nameof(ShowsEmptyBlock));
        OnPropertyChanged(nameof(ShowsHint));
        Request();
    }

    partial void OnCodeChanged(string value)
    {
        Request();
    }

    partial void OnBreaksChanged(int value)
    {
        OnPropertyChanged(nameof(BreaksWord));
        OnPropertyChanged(nameof(NoBreaks));
        OnPropertyChanged(nameof(CountsSpokenName));
    }

    partial void OnFollowsChanged(int value)
    {
        OnPropertyChanged(nameof(FollowsWord));
        OnPropertyChanged(nameof(NoFollows));
        OnPropertyChanged(nameof(CountsSpokenName));
    }

    partial void OnNotCoveredChanged(int value)
    {
        OnPropertyChanged(nameof(NotCoveredWord));
        OnPropertyChanged(nameof(NoNotCovered));
        OnPropertyChanged(nameof(CountsSpokenName));
    }

    /// <summary>
    /// A reason of the body, said in the page's words: each property of the rule's artifact is named as its picker names it
    /// ("statements in body" for <c>statementsInBody</c>).
    /// </summary>
    public string InPageWords(string reason)
    {
        var said = reason;
        foreach(var name in _propertyNames.OrderByDescending(name => name.Length))
            said = Regex.Replace(said, $@"\b{Regex.Escape(name)}\b", RuleCatalog.Words(name));

        return said;
    }

    private static string PlaceholderFor(string artifactWords)
    {
        if(artifactWords == _noArtifactYet)
            return "The code to try the rule on";

        var article = Article(artifactWords);
        return $"{char.ToUpperInvariant(article[0])}{article[1..]} {artifactWords}, or the {Around(artifactWords)} around it";
    }

    private static string Article(string artifactWords)
    {
        return "aeiou".Contains(artifactWords[0], StringComparison.Ordinal) ? "an" : "a";
    }

    private static string Around(string artifactWords)
    {
        return artifactWords == "if statement" ? "method" : artifactWords == "method" ? "class" : "code";
    }

    private void Request()
    {
        _wanted++;
        if(IsBusy)
            return;

        Run();
    }

    // Runs until the answer shown is the answer to the newest text. It is async void on purpose: it starts from a property
    // change and nobody awaits it; a fault must reach the window's thread and be seen, not vanish in a task.
    private async void Run()
    {
        IsBusy = true;
        OnPropertyChanged(nameof(IsBusy));
        try
        {
            while(true)
            {
                var wanted = _wanted;
                var plan = _plan;
                var code = Code;
                TryAnswer? answer = null;
                if(plan != null && !string.IsNullOrWhiteSpace(code))
                    answer = await Task.Run(() => plan.Run(code));

                if(wanted == _wanted)
                {
                    Show(plan, code, answer);
                    return;
                }
            }
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    private void Show(TryPlan? plan, string code, TryAnswer? answer)
    {
        Verdicts = new List<ArtifactVerdict>();
        Breaks = 0;
        Follows = 0;
        NotCovered = 0;
        Message = string.Empty;
        ParseText = string.Empty;
        Describe(plan, code, answer);
        Shown?.Invoke();
    }

    private void Describe(TryPlan? plan, string code, TryAnswer? answer)
    {
        if(ReadsFileName)
        {
            Phase = TryPhase.Empty;
            return;
        }

        if(plan == null)
        {
            Message = _unfinished;
            Phase = TryPhase.Unfinished;
            return;
        }

        if(answer == null || string.IsNullOrWhiteSpace(code))
        {
            Phase = TryPhase.Empty;
            return;
        }

        ShowAnswer(answer);
    }

    private void ShowAnswer(TryAnswer answer)
    {
        if(answer.Result == null)
        {
            Message = answer.Refusal ?? string.Empty;
            Phase = TryPhase.Refused;
            return;
        }

        var result = answer.Result;
        if(result.Status == TryStatus.NeedsBuild)
        {
            Message = Sentence(result.Reason);
            Phase = TryPhase.NeedsBuild;
        }
        else if(result.Status == TryStatus.DoesNotParse)
        {
            ParseText = result.Line == null ? "Does not parse" : $"Does not parse: line {result.Line}";
            Phase = TryPhase.DoesNotParse;
        }
        else if(result.Verdicts.Count == 0)
        {
            Message = $"No {_artifactWords} in this code.";
            Phase = TryPhase.NoArtifact;
        }
        else
        {
            ShowVerdicts(result);
        }
    }

    private void ShowVerdicts(RuleTryResult result)
    {
        Verdicts = result.Verdicts;
        Breaks = result.Count(TryOutcome.BreaksIt);
        Follows = result.Count(TryOutcome.FollowsIt);
        NotCovered = result.Count(TryOutcome.NotCovered);
        Phase = TryPhase.Tried;
    }

    private static string Sentence(string reason)
    {
        var text = reason.Trim().TrimEnd('.');
        return text.Length == 0 ? text : $"{char.ToUpperInvariant(text[0])}{text[1..]}.";
    }
}
