using CF.App.Parts;
using CF.Core.Code;
using CF.Core.Findings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Rules.Page;

/// <summary>
/// The page of a rule that .editorconfig sets: what the rule is, where the file sets it, the lines of the file round that
/// one, and what a push does for each setting the file can hold. It is read-only: nothing here changes the rule or the file.
/// One page is bound to whichever file rule is chosen.
/// </summary>
internal sealed partial class FileRulePageViewModel : ObservableObject
{
    private const string _fileName = ".editorconfig";

    private readonly Action<int> _openFile;

    public FileRulePageViewModel(Action<int> openFile)
    {
        _openFile = openFile;
        Settings = new List<FileSettingRowViewModel>
        {
            new FileSettingRowViewModel("error", EffectKind.Block, "Blocks", "The push is refused."),
            new FileSettingRowViewModel("warning, suggestion", EffectKind.Warn, "Warns", "The push goes through, with a warning."),
            new FileSettingRowViewModel("silent, none", EffectKind.Off, "Off", "The rule is not checked.")
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Id), nameof(Title), nameof(HasTitle), nameof(StateWord), nameof(StateSpokenName), nameof(StateToolTip), nameof(LineHeadText), nameof(LinesSpokenName))]
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
            return Rule?.Main?.Effect ?? "Not set";
        }
    }

    public string StateSpokenName
    {
        get
        {
            return StateWord + StateSuffix;
        }
    }

    public EffectKind Kind
    {
        get
        {
            return Rule?.Main == null ? EffectKind.Unset : PlaceKind(Rule.Main);
        }
    }

    public static EffectKind PlaceKind(FileRulePlace place)
    {
        return place.Severity switch
        {
            Severity.Block => EffectKind.Block,
            Severity.Warn => EffectKind.Warn,
            _ => EffectKind.Off
        };
    }

    public bool IsBlock
    {
        get
        {
            return Kind == EffectKind.Block;
        }
    }

    public bool IsWarn
    {
        get
        {
            return Kind == EffectKind.Warn;
        }
    }

    public bool IsOff
    {
        get
        {
            return !IsBlock && !IsWarn;
        }
    }

    public string StateSuffix
    {
        get
        {
            var count = Rule?.Exceptions.Count > 0 ? ", " + Rule.ExceptionCount : string.Empty;
            return $" for all {Rule?.Language} files{count}" + (Rule?.Main == null ? " in the file" : ", set in the file");
        }
    }

    public string StateToolTip
    {
        get
        {
            if(Rule?.Main == null)
                return StateSpokenName;

            var lines = Rule.Exceptions.Select(place => place.Line.ToString()).ToList();
            var exceptions = lines.Count switch
            {
                0 => string.Empty,
                1 => $" Line {lines[0]} sets it differently for some files.",
                _ => $" Lines {string.Join(", ", lines.SkipLast(1))} and {lines[^1]} set it differently for some files."
            };
            return $"{StateWord} for all {Rule.Language} files: {Rule.Setting}, on line {Rule.Line} of {_fileName}.{exceptions} Change it in the file.";
        }
    }

    public string LineHeadText
    {
        get
        {
            if(!HasExceptions && Rule?.Line is int line)
                return $"line {line} sets this rule";

            var count = Places.Count;
            var scope = Rule?.Main == null ? ", for some files only" : string.Empty;
            var order = count > 1 ? " A later line overrides an earlier one." : string.Empty;
            return $"{count} {(count == 1 ? "line sets" : "lines set")} this rule{scope}.{order}";
        }
    }

    public bool HasExceptions
    {
        get { return Rule?.Exceptions.Count > 0; }
    }

    public FilePlaceViewModel? FirstPlace
    {
        get { return Places.Count > 0 ? Places[0] : null; }
    }

    public string LinesSpokenName
    {
        get
        {
            var order = Places.Count > 1 ? " A later line overrides an earlier one." : string.Empty;
            return $"Lines of {_fileName} that set this rule, {Places.Count} {(Places.Count == 1 ? "place" : "places")}.{order}";
        }
    }

    /// <summary>
    /// The places for the lines of the file round the rule's. They are made once.
    /// </summary>
    public IReadOnlyList<FileLineViewModel> Lines
    {
        get
        {
            return Places.Count > 0 ? Places[0].Lines : [];
        }
    }

    public IReadOnlyList<FilePlaceViewModel> Places { get; private set; } = [];

    /// <summary>
    /// The three rows of the table. They are made once.
    /// </summary>
    public IReadOnlyList<FileSettingRowViewModel> Settings { get; }

    /// <summary>
    /// Binds the page to this rule: the same places and rows say what this rule's file says.
    /// </summary>
    public void Show(FileRule rule)
    {
        Places = rule.Places.Select(place => new FilePlaceViewModel(rule, place, _openFile)).ToList();
        Rule = rule;
        for(var index = 0; index < Settings.Count; index++)
        {
            var row = Settings[index];
            row.IsCurrent = IsCurrent(rule, index);
            var patterns = rule.Exceptions.Where(place => place.Effect == row.EffectWord).Select(place => place.Pattern).ToList();
            row.Tag = row.IsCurrent && rule.Exceptions.Count > 0 ? $"For all {rule.Language} files" : string.Empty;
            if(patterns.Count > 0)
                row.Tag += (row.IsCurrent ? ", and for " : "For ") + string.Join(", ", patterns);
        }

        OnPropertyChanged(string.Empty);
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