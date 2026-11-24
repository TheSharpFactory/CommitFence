using CF.App.Parts;
using CF.Core.Code;
using CF.Core.Findings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// The page of a rule that .editorconfig sets: what the rule is, where the file sets it, the lines of the file round that
/// one, and what a push does for each setting the file can hold. It is read-only: nothing here changes the rule or the file.
/// One page is bound to whichever file rule is chosen.
/// </summary>
internal sealed partial class FileRulePageViewModel : ObservableObject
{
    private const string _fileName = ".editorconfig";

    private const int _linePlaces = 7;

    private readonly Action _openFile;

    public FileRulePageViewModel(Action openFile)
    {
        _openFile = openFile;
        Lines = Enumerable.Range(0, _linePlaces).Select(_ => new FileLineViewModel()).ToList();
        Settings = new List<FileSettingRowViewModel>
        {
            new FileSettingRowViewModel("error", EffectKind.Block, "Blocks", "The push is refused."),
            new FileSettingRowViewModel("warning, suggestion", EffectKind.Warn, "Warns", "The push goes through, with a warning."),
            new FileSettingRowViewModel("silent, none", EffectKind.Off, "Off", "The rule is not checked.")
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Id), nameof(Title), nameof(HasTitle), nameof(StateWord), nameof(StateSpokenName), nameof(StateToolTip), nameof(LineHeadText), nameof(OpenAtLineText), nameof(LinesSpokenName))]
    public partial FileRule? Rule { get; set; }

    /// <summary>
    /// The id, empty while no rule is bound.
    /// </summary>
    public string Id
    {
        get
        {
            return Rule?.Id ?? string.Empty;
        }
    }

    public string Title
    {
        get
        {
            return Rule?.Title ?? string.Empty;
        }
    }

    public bool HasTitle
    {
        get
        {
            return Rule?.Title != null;
        }
    }

    /// <summary>
    /// The first word of the state: "On", "Off", or the command line's word for a state with no word of its own.
    /// </summary>
    public string StateWord
    {
        get
        {
            return Describe(Rule?.State);
        }
    }

    public string StateSpokenName
    {
        get
        {
            return StateWord + ", set in the file";
        }
    }

    public string StateToolTip
    {
        get
        {
            if(Rule == null)
                return string.Empty;

            var word = Rule.State == FileRuleState.On ? "On" : Rule.State == FileRuleState.Off ? "Off" : "Set";
            return $"{word} in {_fileName}: {Rule.Setting}, on line {Rule.Line}. Change it in the file.";
        }
    }

    public string LineHeadText
    {
        get
        {
            return Rule == null ? string.Empty : $"line {Rule.Line} sets this rule";
        }
    }

    public string OpenAtLineText
    {
        get
        {
            return Rule == null ? string.Empty : $"Open at line {Rule.Line}";
        }
    }

    public string LinesSpokenName
    {
        get
        {
            return Rule == null ? string.Empty : $"Lines of {_fileName} round line {Rule.Line}, which sets this rule";
        }
    }

    /// <summary>
    /// The places for the lines of the file round the rule's. They are made once.
    /// </summary>
    public IReadOnlyList<FileLineViewModel> Lines { get; }

    /// <summary>
    /// The three rows of the table. They are made once.
    /// </summary>
    public IReadOnlyList<FileSettingRowViewModel> Settings { get; }

    /// <summary>
    /// Binds the page to this rule: the same places and rows say what this rule's file says.
    /// </summary>
    public void Show(FileRule rule)
    {
        Rule = rule;
        for(var index = 0; index < Lines.Count; index++)
            Lines[index].Take(index < rule.Lines.Count ? rule.Lines[index] : null, index < rule.Lines.Count && rule.Lines[index].Number == rule.Line);

        for(var index = 0; index < Settings.Count; index++)
            Settings[index].IsCurrent = IsCurrent(rule, index);
    }

    [RelayCommand]
    private void OpenFile()
    {
        _openFile();
    }

    private static string Describe(FileRuleState? state)
    {
        if(state == FileRuleState.On)
            return "On";

        if(state == FileRuleState.Off)
            return "Off";

        return state == FileRuleState.OwnDefault ? "default" : "not understood";
    }

    private static bool IsCurrent(FileRule rule, int index)
    {
        if(rule.State == FileRuleState.Off)
            return index == 2;

        if(rule.State != FileRuleState.On)
            return false;

        return index == (rule.Severity == Severity.Block ? 0 : 1);
    }
}
