using CF.Core.Findings;
using CF.Core.Rules;

namespace CF.Cli.Commands;

/// <summary>
/// Reads the arguments of the rules command one at a time: options anywhere, the rest in order. Options a subcommand
/// does not use are refused rather than ignored.
/// </summary>
internal sealed class RulesArgumentsReader
{
    private const string _optionStart = "--";
    private const string _standardInput = "-";

    private readonly string[] _args;
    private readonly List<string> _positionals = new List<string>();
    private readonly RuleFieldArguments _fields = new RuleFieldArguments();
    private string? _settingsPath;
    private string? _before;
    private int _position;

    public RulesArgumentsReader(string[] args)
    {
        _args = args;
    }

    public RulesArguments Read()
    {
        for(_position = 1; _position < _args.Length; _position++)
        {
            var error = ReadCurrent();
            if(error != null)
                return Fail(error);
        }

        return Build();
    }

    private string? ReadCurrent()
    {
        var argument = _args[_position];
        if(!argument.StartsWith(_optionStart, StringComparison.Ordinal))
        {
            _positionals.Add(argument);
            return null;
        }

        switch(argument)
        {
            case "--forbid":
                _fields.Forbid = true;
                return null;
            case "--no-where":
                _fields.NoWhere = true;
                return null;
            case "--no-paths":
                _fields.NoPaths = true;
                return null;
            case "--no-message":
                _fields.NoMessage = true;
                return null;
            case "--no-why":
                _fields.NoWhy = true;
                return null;
            default:
                return ReadWithValue(argument);
        }
    }

    private string? ReadWithValue(string option)
    {
        var known = new[] { "--settings", "--language", "--for", "--severity", "--title", "--where", "--require", "--paths", "--message", "--why", "--before" };
        if(!known.Contains(option))
            return $"'{option}' is not understood here.";

        if(_position + 1 >= _args.Length)
            return $"{option} needs a value.";

        _position++;
        return Store(option, _args[_position]);
    }

    private string? Store(string option, string value)
    {
        switch(option)
        {
            case "--settings":
                return StoreOnce(option, value, _settingsPath, text => _settingsPath = text);
            case "--before":
                return StoreOnce(option, value, _before, text => _before = text);
            case "--language":
                return StoreOnce(option, value, _fields.Language, text => _fields.Language = text);
            case "--for":
                return StoreOnce(option, value, _fields.ArtifactType, text => _fields.ArtifactType = text);
            case "--title":
                return StoreOnce(option, value, _fields.Title, text => _fields.Title = text);
            case "--message":
                return StoreOnce(option, value, _fields.Message, text => _fields.Message = text);
            case "--why":
                return StoreOnce(option, value, _fields.Why, text => _fields.Why = text);
            case "--paths":
                return StorePaths(value);
            case "--severity":
                return StoreSeverity(value);
            default:
                return StoreCondition(option, value);
        }
    }

    private static string? StoreOnce(string option, string value, string? current, Action<string> store)
    {
        if(current != null)
            return $"{option} is given twice.";

        store(value);
        return null;
    }

    private string? StorePaths(string value)
    {
        if(_fields.Paths != null)
            return "--paths is given twice.";

        var paths = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if(paths.Count == 0)
            return "--paths needs at least one pattern. To clear the paths of a rule use --no-paths.";

        _fields.Paths = paths;
        return null;
    }

    private string? StoreSeverity(string value)
    {
        var severity = ReadSeverity(value);
        if(severity == null)
            return $"'{value}' is not a severity. Use block or warn.";

        _fields.Severity = severity;
        return null;
    }

    private string? StoreCondition(string option, string value)
    {
        var index = value.AsSpan().IndexOfAny("=~<>!");
        var (comparison, width) = index > 0 ? ReadComparison(value.AsSpan(index)) : (Comparison.Is, 0);
        if(width == 0)
            return $"{option} takes a condition written property=value, such as braces=false, or use <=, >=, != or ~.";

        var property = value[..index];
        var operand = value[(index + width)..];
        var values = comparison is Comparison.Is or Comparison.IsNot ? operand.Split(',') : new[] { operand };
        if(string.IsNullOrWhiteSpace(property) || values.Any(string.IsNullOrWhiteSpace))
            return $"{option} '{value}' has an empty property or an empty value. Write property=value, or property=a,b.";

        if(comparison == Comparison.Is && values.Length > 1)
            comparison = Comparison.IsOneOf;

        var draft = new ConditionDraft(property, values, comparison);
        if(option == "--where")
            (_fields.Where ??= new List<ConditionDraft>()).Add(draft);
        else
            (_fields.Require ??= new List<ConditionDraft>()).Add(draft);

        return null;
    }

    private static (Comparison Comparison, int Width) ReadComparison(ReadOnlySpan<char> text)
    {
        return text switch
        {
            ['=', ..] => (Comparison.Is, 1),
            ['~', ..] => (Comparison.Matches, 1),
            ['<', '=', ..] => (Comparison.AtMost, 2),
            ['>', '=', ..] => (Comparison.AtLeast, 2),
            ['!', '=', ..] => (Comparison.IsNot, 2),
            _ => (Comparison.Is, 0)
        };
    }

    private static Severity? ReadSeverity(string text)
    {
        if(text == "block")
            return Severity.Block;

        return text == "warn" ? Severity.Warn : null;
    }

    private RulesArguments Build()
    {
        if(_positionals.Count == 0)
            return Fail("Use rules list, show, enable, disable, severity, add, set, copy, remove or try.");

        var subcommand = _positionals[0];
        var error = CheckSubcommand(subcommand) ?? CheckBefore(subcommand);
        if(error != null)
            return Fail(error);

        var ruleId = _positionals.Count > 1 ? _positionals[1] : string.Empty;
        return new RulesArguments(subcommand, ruleId, _settingsPath, ReadSeverityArgument(subcommand), ReadFileArgument(subcommand), _fields, _before, subcommand == "copy" ? _positionals[2] : null, null);
    }

    private string? CheckSubcommand(string subcommand)
    {
        switch(subcommand)
        {
            case "list":
                return Expect(subcommand, 0, 0, false, "rules list takes no other argument.");
            case "show":
            case "enable":
            case "disable":
            case "remove":
                return Expect(subcommand, 1, 1, false, $"rules {subcommand} takes one rule id.");
            case "severity":
                return CheckSeverity();
            case "try":
                return Expect(subcommand, 1, 2, false, "rules try takes a rule id and, when the code is not on standard input, a file.");
            case "copy":
                return Expect(subcommand, 2, 2, false, "rules copy takes the id of the rule to copy and the new rule id.");
            case "add":
                return CheckAdd();
            case "set":
                return CheckSet();
            default:
                return $"'{subcommand}' is not a rules command. Use list, show, enable, disable, severity, add, set, copy, remove or try.";
        }
    }

    private string? Expect(string subcommand, int minimum, int maximum, bool takesFields, string message)
    {
        var count = _positionals.Count - 1;
        if(count < minimum || count > maximum)
            return message;

        if(!takesFields && !_fields.IsEmpty)
            return $"rules {subcommand} does not take options for the parts of a rule.";

        return null;
    }

    private string? CheckBefore(string subcommand)
    {
        return _before != null && subcommand != "add" ? $"rules {subcommand} does not take --before: only rules add puts a rule at a place." : null;
    }

    private string? CheckSeverity()
    {
        var error = Expect("severity", 2, 2, false, "rules severity takes a rule id and block or warn.");
        if(error != null)
            return error;

        return ReadSeverity(_positionals[2]) == null ? $"'{_positionals[2]}' is not a severity. Use block or warn." : null;
    }

    private string? CheckSet()
    {
        var error = Expect("set", 1, 1, true, "rules set takes one rule id and the options to change.");
        if(error != null)
            return error;

        if(_fields.Severity != null)
            return "rules set does not change the severity. Use rules severity ID block or warn.";

        if(_fields.Language != null)
            return "rules set does not take --language: the language of a rule cannot be changed.";

        return _fields.IsEmpty ? "rules set needs at least one option to change." : CheckClearedParts();
    }

    private string? CheckClearedParts()
    {
        if(_fields.NoWhere && _fields.Where != null)
            return "--no-where and --where cannot be given together.";

        if(_fields.NoPaths && _fields.Paths != null)
            return "--no-paths and --paths cannot be given together.";

        if(_fields.NoMessage && _fields.Message != null)
            return "--no-message and --message cannot be given together.";

        return _fields.NoWhy && _fields.Why != null ? "--no-why and --why cannot be given together." : null;
    }

    private string? CheckAdd()
    {
        var error = Expect("add", 1, 1, true, "rules add takes one new rule id and the options of the rule.");
        if(error != null)
            return error;

        if(_fields.NoWhere || _fields.NoPaths || _fields.NoMessage || _fields.NoWhy)
            return "rules add does not take --no-where, --no-paths, --no-message or --no-why: a new rule has none of these to clear.";

        if(_fields.ArtifactType == null || _fields.Title == null)
            return "rules add needs --for (the artifact type) and --title, and --require or --forbid.";

        return null;
    }

    private Severity? ReadSeverityArgument(string subcommand)
    {
        return subcommand == "severity" ? ReadSeverity(_positionals[2]) : null;
    }

    private string? ReadFileArgument(string subcommand)
    {
        if(subcommand != "try" || _positionals.Count < 3 || _positionals[2] == _standardInput)
            return null;

        return _positionals[2];
    }

    private static RulesArguments Fail(string error)
    {
        return new RulesArguments(string.Empty, string.Empty, null, null, null, new RuleFieldArguments(), null, null, error);
    }
}