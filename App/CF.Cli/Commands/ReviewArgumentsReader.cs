namespace CF.Cli.Commands;

/// <summary>
/// Reads the arguments of the review command once, into what was given, and only then, when the form is known, says
/// what the plain arguments mean: the files of --files, the one target of --all, and nothing in any other form.
/// </summary>
internal sealed class ReviewArgumentsReader
{
    private const string _settingsOption = "--settings";
    private const string _filesOption = "--files";
    private const string _stagedOption = "--staged";
    private const string _quickOption = "--quick";
    private const string _hookOption = "--hook";
    private const string _pushOption = "--push";
    private const string _baseOption = "--base";
    private const string _allOption = "--all";
    private const string _headOption = "--head";
    private const string _formatOption = "--format";
    private const string _outputOption = "--output";
    private const string _textFormat = "text";
    private const string _promptFormat = "prompt";
    private const string _sarifFormat = "sarif";
    private const string _prePushHook = "pre-push";
    private const string _defaultHead = "HEAD";
    private const int _hookValueCount = 3;

    private static readonly string[] _forms = { _filesOption, _stagedOption, _hookOption, _pushOption, _baseOption, _allOption };

    private static readonly string[] _optionsWithValue = { _settingsOption, _baseOption, _headOption, _formatOption, _outputOption };

    private readonly string[] _args;
    private readonly bool _allowPushHead;
    private int _position;

    public ReviewArgumentsReader(string[] args, bool allowPushHead)
    {
        _args = args;
        _allowPushHead = allowPushHead;
    }

    public ReviewArgumentsResult Read()
    {
        var given = new GivenArguments();
        for(_position = 1; _position < _args.Length; _position++)
        {
            var error = ReadCurrent(given);
            if(error != null)
                return ReviewArgumentsResult.Wrong(error);
        }

        var decided = Decide(given);
        if(decided.Arguments != null)
        {
            decided.Arguments.AsPrompt = given.Values.TryGetValue(_formatOption, out var format) && format == _promptFormat;
            decided.Arguments.AsSarif = format == _sarifFormat;
            decided.Arguments.OutputPath = given.Values.GetValueOrDefault(_outputOption);
            decided.Arguments.Quick = given.PreCommit || given.Options.Contains(_quickOption);
        }

        return decided;
    }

    private string? ReadCurrent(GivenArguments given)
    {
        var argument = _args[_position];
        if(!argument.StartsWith("--", StringComparison.Ordinal))
        {
            given.Plain.Add(argument);
            return null;
        }

        if(!_forms.Contains(argument) && !_optionsWithValue.Contains(argument) && (argument != _quickOption || _allowPushHead))
            return $"'{argument}' is not understood here.";

        if(!given.Options.Add(argument))
            return $"{argument} was given twice.";

        if(argument == _hookOption)
            return ReadHook(given);

        return _optionsWithValue.Contains(argument) ? ReadValue(given, argument) : null;
    }

    private string? ReadValue(GivenArguments given, string option)
    {
        // an option where the value should stand is a forgotten value, never the value
        var isWithoutValue = _position + 1 >= _args.Length || _args[_position + 1].StartsWith("--", StringComparison.Ordinal);
        if(isWithoutValue)
            return $"{option} needs one value.";

        _position++;
        given.Values[option] = _args[_position];
        return null;
    }

    private string? ReadHook(GivenArguments given)
    {
        if(!_allowPushHead && _position + 1 < _args.Length && _args[_position + 1] == "pre-commit")
        {
            given.PreCommit = true;
            _position++;
            return null;
        }

        if(_position + _hookValueCount >= _args.Length || _args[_position + 1] != _prePushHook)
            return "--hook needs pre-push, the remote's name and the remote's address, as git gives them to the hook.";

        given.RemoteName = _args[_position + 2];
        given.RemoteUrl = _args[_position + 3];
        _position += _hookValueCount;
        return null;
    }

    private ReviewArgumentsResult Decide(GivenArguments given)
    {
        if(given.Values.TryGetValue(_formatOption, out var format) && format != _textFormat && format != _promptFormat && format != _sarifFormat)
            return ReviewArgumentsResult.Wrong($"--format knows three words: {_textFormat}, the default, {_promptFormat} and {_sarifFormat}; '{format}' is not one of them.");

        var besideAll = FindWhatStandsBesideAll(given);
        if(besideAll != null)
            return ReviewArgumentsResult.Wrong(besideAll);

        var forms = _forms.Where(given.Options.Contains).ToList();
        if(forms.Count != 1)
            return ReviewArgumentsResult.Wrong("review needs exactly one of --files, --staged, --hook, --push, --base or --all.");

        if(given.Options.Contains(_headOption) && !given.Options.Contains(_baseOption) && !(_allowPushHead && forms[0] == _pushOption))
            return ReviewArgumentsResult.Wrong("--head needs --base.");

        if(given.Options.Contains(_quickOption) && forms[0] is not (_stagedOption or _filesOption) && !given.PreCommit)
            return ReviewArgumentsResult.Wrong("--quick needs --staged, --files or --hook pre-commit.");

        return DecideForm(given, forms[0]);
    }

    private static ReviewArgumentsResult DecideForm(GivenArguments given, string form)
    {
        given.Values.TryGetValue(_settingsOption, out var settingsPath);
        switch(form)
        {
            case _filesOption:
                return given.Plain.Count == 0
                    ? ReviewArgumentsResult.Wrong("review needs --files with at least one path.")
                    : ReviewArgumentsResult.Valid(ReviewArguments.ForFiles(given.Plain, settingsPath));
            case _allOption:
                return DecideAll(given, settingsPath);
            default:
                return RefusePlainArguments(given) ?? ReviewArgumentsResult.Valid(CreateForm(given, form, settingsPath));
        }
    }

    private static ReviewArgumentsResult DecideAll(GivenArguments given, string? settingsPath)
    {
        if(given.Plain.Count > 1)
            return ReviewArgumentsResult.Wrong($"--all takes at most one folder or project file, and got '{given.Plain[0]}' and '{given.Plain[1]}'.");

        return ReviewArgumentsResult.Valid(ReviewArguments.ForAll(given.Plain.Count == 0 ? string.Empty : given.Plain[0], settingsPath));
    }

    private static ReviewArguments CreateForm(GivenArguments given, string form, string? settingsPath)
    {
        switch(form)
        {
            case _pushOption:
                return ReviewArguments.ForPush(settingsPath, given.Values.GetValueOrDefault(_headOption, string.Empty));
            case _hookOption:
                return given.PreCommit ? ReviewArguments.ForStaged(settingsPath) : ReviewArguments.ForHook(given.RemoteName, given.RemoteUrl, settingsPath);
            case _baseOption:
                given.Values.TryGetValue(_headOption, out var head);
                return ReviewArguments.ForRange(given.Values[_baseOption], head ?? _defaultHead, settingsPath);
            default:
                return ReviewArguments.ForStaged(settingsPath);
        }
    }

    /// <summary>
    /// A plain argument in a form that takes none is not understood.
    /// </summary>
    private static ReviewArgumentsResult? RefusePlainArguments(GivenArguments given)
    {
        return given.Plain.Count == 0 ? null : ReviewArgumentsResult.Wrong($"'{given.Plain[0]}' is not understood here.");
    }

    /// <summary>
    /// What --all cannot be combined with: it stands alone, and a hook never runs it.
    /// </summary>
    private static string? FindWhatStandsBesideAll(GivenArguments given)
    {
        if(!given.Options.Contains(_allOption))
            return null;

        if(given.Options.Contains(_hookOption) || given.Options.Contains(_pushOption))
            return "--all is never run by a hook: a hook reviews what is being pushed, and the whole of a folder is reviewed on command.";

        var other = new[] { _stagedOption, _baseOption, _filesOption, _headOption }.FirstOrDefault(given.Options.Contains);
        return other == null ? null : $"--all stands alone: it cannot be combined with {other}.";
    }

    /// <summary>
    /// What was given, before anyone says what it means.
    /// </summary>
    private sealed class GivenArguments
    {
        public HashSet<string> Options { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public List<string> Plain { get; } = new List<string>();

        public bool PreCommit { get; set; }

        public string RemoteName { get; set; } = string.Empty;

        public string RemoteUrl { get; set; } = string.Empty;
    }
}
