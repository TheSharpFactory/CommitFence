using CF.App.Parts;
using CF.Core.Code;
using CF.Core.Findings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules;

/// <summary>
/// The row of a rule that .editorconfig sets: its id, its title, what the file's setting does to a push as a fact, and the
/// line of the file that sets it. It holds no control that changes the rule: the product does not change that file.
/// </summary>
internal sealed partial class FileRuleRowViewModel : RuleListItemViewModel
{
    private const string _fileName = ".editorconfig";

    private readonly Action<int> _openFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RingOnRow), nameof(RingOnLine))]
    public partial RowPart Part { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RingOnRow), nameof(RingOnLine))]
    public partial bool IsRingVisible { get; set; }

    public FileRuleRowViewModel(FileRule rule, Action<int> openFile)
    {
        _openFile = openFile;
        Rule = rule;
        Id = rule.Id;
        (IdFamily, IdNumber) = SplitId(rule.Id);
    }

    /// <summary>
    /// The rule as the body listed it.
    /// </summary>
    public FileRule Rule { get; private set; }

    public string Id { get; }

    public string IdFamily { get; }

    public string IdNumber { get; }

    public bool HasExceptions
    {
        get { return Rule.Exceptions.Count > 0; }
    }

    public bool IsUnset
    {
        get
        {
            return Rule.Main == null;
        }
    }

    public string UnsetTip
    {
        get
        {
            return $"Not set for all {Rule.Language} files in {_fileName}, so the rule's own default holds for them. Change it in the file.";
        }
    }

    public IEnumerable<ExceptionLine> ExceptionLines
    {
        get
        {
            return Rule.ExceptionGroups().Select(group => new ExceptionLine(group));
        }
    }

    public string Title
    {
        get
        {
            return Rule.Title ?? string.Empty;
        }
    }

    /// <summary>
    /// The rule is on in the file: the mark and the word of its effect are drawn.
    /// </summary>
    public bool ShowsEffect
    {
        get
        {
            return Rule.State == FileRuleState.On;
        }
    }

    /// <summary>
    /// The file switches the rule off: "Off in the file" stands across the effect's place and the line's.
    /// </summary>
    public bool IsOffInFile
    {
        get
        {
            return Rule.State == FileRuleState.Off;
        }
    }

    /// <summary>
    /// The file's setting says nothing the product can state as an effect (its own default, or a word the build does not
    /// understand): the command line's word stands in the effect's place.
    /// </summary>
    public bool ShowsStateWord
    {
        get
        {
            return !IsUnset && Rule.State is FileRuleState.OwnDefault or FileRuleState.NotUnderstood;
        }
    }

    public bool MarkIsBlock
    {
        get
        {
            return ShowsEffect && Rule.Severity == Severity.Block;
        }
    }

    public bool MarkIsWarn
    {
        get
        {
            return ShowsEffect && Rule.Severity == Severity.Warn;
        }
    }

    public EffectKind EffectKind
    {
        get
        {
            return Rule.Severity == Severity.Block ? EffectKind.Block : EffectKind.Warn;
        }
    }

    public string EffectWord
    {
        get
        {
            return Rule.Severity == Severity.Block ? "Blocks" : "Warns";
        }
    }

    /// <summary>
    /// The command line's word for a state with no sentence of its own: "default", "not understood".
    /// </summary>
    public string StateWord
    {
        get
        {
            return Rule.ListedPlace?.Effect ?? string.Empty;
        }
    }

    /// <summary>
    /// The title is weight 400 in ink-2 for a rule the file switches off.
    /// </summary>
    public bool RuleIsOff
    {
        get
        {
            return IsOffInFile;
        }
    }

    /// <summary>
    /// The line of the file that sets the rule is a link in the last place, except on a rule the file switches off.
    /// </summary>
    public bool ShowsLine
    {
        get
        {
            return Rule.Line != null;
        }
    }

    public string LineText
    {
        get
        {
            return $"line {Rule.Line}";
        }
    }

    public string LineToolTip
    {
        get
        {
            return $"Line {Rule.Line} of {_fileName} sets this rule. Open the file there.";
        }
    }

    public string LineSpokenName
    {
        get
        {
            return $"Set on line {Rule.Line} of {_fileName}. Open the file there.";
        }
    }

    /// <summary>
    /// What a row says when it is shown to the pointer: the whole title, and for a rule the file switches off, how.
    /// </summary>
    public string OffToolTip
    {
        get
        {
            if(Rule.Main?.IsOffByStrictness == true)
                return $"Off under the current strictness. {_fileName} sets it to {Rule.Setting} on line {Rule.Line}.";

            return $"Off for all {Rule.Language} files: {Rule.Setting}, on line {Rule.Line} of {_fileName}. Change it in the file.";
        }
    }

    public string StateToolTip
    {
        get
        {
            return Rule.State == FileRuleState.OwnDefault
                ? $"{_fileName} sets this rule to default, on line {Rule.Line}: the rule's own default applies, and what it does to a push is not known here."
                : $"{_fileName} sets this rule to '{Rule.Setting}', on line {Rule.Line}, and the build does not understand that word, so it has no effect the product can state.";
        }
    }

    public bool RingOnRow
    {
        get
        {
            return IsRingVisible && Part == RowPart.Row;
        }
    }

    public bool RingOnLine
    {
        get
        {
            return IsRingVisible && Part == RowPart.Line;
        }
    }

    /// <summary>
    /// What a screen reader says for the whole row.
    /// </summary>
    private string MainSpokenName
    {
        get
        {
            var title = Rule.Title == null ? string.Empty : $", {Rule.Title}";
            if(IsUnset)
                return $"{Id}{title}. From {_fileName}, on and off set in the file. Not set for all {Rule.Language} files.";

            if(Rule.Main?.IsOffByStrictness == true)
                return $"{Id}{title}. From {_fileName}. Off under the current strictness.";

            if(IsOffInFile)
                return $"{Id}{title}. From {_fileName}, on and off set in the file. Off in the file.";

            if(ShowsStateWord)
                return $"{Id}{title}. From {_fileName}, on and off set in the file. {StateWord}. Set on line {Rule.Line} of {_fileName}.";

            return $"{Id}{title}. From {_fileName}, on and off set in the file. On. {(Rule.Severity == Severity.Block ? "Blocks a push" : "Only warns")}. Set on line {Rule.Line} of {_fileName}.";
        }
    }

    public string SpokenName
    {
        get
        {
            return Rule.Note == null ? SpokenNameOfSettings : $"{SpokenNameOfSettings} {Rule.Note}";
        }
    }

    private string SpokenNameOfSettings
    {
        get
        {
            if(Rule.Exceptions.Count == 0)
                return MainSpokenName;

            var summary = Rule.ExceptionLines().Select(line => line.Replace("Blocks for ", "it blocks a push for ", StringComparison.Ordinal)
                .Replace("Warns for ", "it only warns for ", StringComparison.Ordinal).Replace("Off for ", "it is off for ", StringComparison.Ordinal));
            var count = Rule.Exceptions.Count;
            return $"{MainSpokenName} Set {(IsUnset ? "only" : "differently")} for {count} {(count == 1 ? "pattern" : "patterns")}: {string.Join("; ", summary)}.";
        }
    }

    /// <summary>
    /// Takes in what the file says now, keeping this row as the object the list holds.
    /// </summary>
    public void Update(FileRule rule)
    {
        Rule = rule;
        OnPropertyChanged(string.Empty);
    }

    public void MoveRight()
    {
        if(ShowsLine)
            Part = RowPart.Line;
    }

    public void MoveLeft()
    {
        Part = RowPart.Row;
    }

    [RelayCommand]
    private void OpenFile()
    {
        if(Rule.Line is int line)
            _openFile(line);
    }

    private static (string Family, string Number) SplitId(string id)
    {
        var letters = id.TakeWhile(char.IsLetter).Count();
        return (id.Substring(0, letters), id.Substring(letters));
    }
}