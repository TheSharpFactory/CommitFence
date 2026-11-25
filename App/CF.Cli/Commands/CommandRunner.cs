using CF.Core.Changes;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Picks the command the arguments name and runs it. Anything it does not know is wrong use.
/// </summary>
internal sealed class CommandRunner
{
    /// <summary>
    /// The name of the command that reviews a change.
    /// </summary>
    internal const string ReviewCommandName = "review";

    /// <summary>
    /// The name of the command that updates the program.
    /// </summary>
    internal const string UpdateCommandName = "update";

    /// <summary>The name of the command that checks readiness for pushes.</summary>
    internal const string DoctorCommandName = "doctor";

    private readonly IProductInfoService _productInfo;
    private readonly IRuleService _ruleService;
    private readonly IReviewService _reviewService;
    private readonly ISetupService _setupService;
    private readonly IUpdater _updater;
    private readonly IDoctorService? _doctor;
    private readonly ISettingsService? _settings;
    private readonly IStructureService? _structure;
    private readonly IClock? _clock;
    private readonly IExemptionService? _exemptions;
    private readonly IStatsService? _stats;
    private readonly CommandEnvironment _environment;
    private readonly string _programPath;
    private readonly string _currentFolder;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly Dictionary<string, Func<string[], ExitCode>> _commands;

    public CommandRunner(IProductInfoService productInfo, IRuleService ruleService, IReviewService reviewService, ISetupService setupService, IUpdater updater, CommandEnvironment environment, IDoctorService? doctor = null, ISettingsService? settings = null, IStructureService? structure = null, IClock? clock = null, IExemptionService? exemptions = null, IStatsService? stats = null)
    {
        _productInfo = productInfo;
        _ruleService = ruleService;
        _reviewService = reviewService;
        _setupService = setupService;
        _updater = updater;
        _doctor = doctor;
        _settings = settings;
        _structure = structure;
        _clock = clock;
        _exemptions = exemptions;
        _stats = stats;
        _environment = environment;
        _currentFolder = environment.CurrentFolder;
        _programPath = environment.ProgramPath;
        _output = environment.Output;
        _error = environment.Error;
        _commands = CreateCommands();
    }

    /// <summary>
    /// Runs the command the first argument names; each command checks the arguments it takes. No arguments, or a
    /// command that is not known, is wrong use.
    /// </summary>
    public ExitCode Run(string[] args)
    {
        if(args.Length > 0 && _commands.TryGetValue(args[0], out var command))
            return command(args);

        return Fail(UsageText.Text);
    }

    private Dictionary<string, Func<string[], ExitCode>> CreateCommands()
    {
        return new Dictionary<string, Func<string[], ExitCode>>(StringComparer.Ordinal)
        {
            ["--help"] = Alone(ShowUsage),
            ["-h"] = Alone(ShowUsage),
            ["--version"] = Alone(() => new VersionCommand(_productInfo, _output).Run()),
            ["init"] = Alone(RunInit),
            [DoctorCommandName] = Alone(RunDoctor),
            ["stats"] = args => new StatsCommand(_stats ?? throw new InvalidOperationException("Stats is not configured."), _environment).Run(args),
            ["install"] = RunInstall,
            [UpdateCommandName] = RunUpdate,
            ["catalog"] = RunCatalog,
            ["rules"] = args => new RulesRunner(_ruleService, _environment).Run(args),
            ["settings"] = args => new SettingsCommand(_settings ?? throw new InvalidOperationException("Settings is not configured."), _environment).Run(args),
            ["structure"] = args => new StructureCommand(_structure ?? throw new InvalidOperationException("Structure is not configured."), _clock!, _environment).Run(args),
            ["exemptions"] = args => new ExemptionsCommand(_exemptions ?? throw new InvalidOperationException("Exemptions is not configured."), _environment).Run(args),
            [ReviewCommandName] = RunReview
        };
    }

    /// <summary>
    /// A command that takes no arguments: given any, it is wrong use.
    /// </summary>
    private Func<string[], ExitCode> Alone(Func<ExitCode> command)
    {
        return args => args.Length == 1 ? command() : Fail(UsageText.Text);
    }

    private ExitCode ShowUsage()
    {
        _output.WriteLine(UsageText.Text);
        return ExitCode.Passed;
    }

    private ExitCode RunReview(string[] args)
    {
        var parsed = ReviewArguments.Parse(args);
        var arguments = parsed.Arguments;
        if(arguments == null)
            return Fail(parsed.Error + Environment.NewLine + UsageText.Text);

        var settingsPath = Path.GetFullPath(arguments.SettingsPath ?? SettingsFileName.Default, _currentFolder);
        try
        {
            return RunReviewCommand(arguments, settingsPath);
        }
        catch(ReviewTargetException exception)
        {
            return Fail(exception.Message);
        }
        catch(SettingsException exception)
        {
            var shownPath = IsSettingsReadFromTree(arguments) ? SettingsFileName.Default + " (in the tree under review)" : settingsPath;
            return Fail(SettingsFileName.FormatError(shownPath, exception));
        }
    }

    private ExitCode RunReviewCommand(ReviewArguments arguments, string settingsPath)
    {
        if(arguments.Mode == ReviewMode.Files)
            return new ReviewCommand(_reviewService, _ruleService, _environment, settingsPath, arguments).Run();

        var givenPath = arguments.SettingsPath == null ? null : settingsPath;
        return new ChangesReviewCommand(_productInfo, _reviewService, _ruleService, _environment, givenPath, arguments).Run();
    }

    private static bool IsSettingsReadFromTree(ReviewArguments arguments)
    {
        return arguments.Mode != ReviewMode.Files && arguments.SettingsPath == null;
    }

    private ExitCode RunInstall(string[] args)
    {
        var arguments = InstallArguments.Parse(args);
        if(arguments.Options == null)
            return Fail(arguments.Error + Environment.NewLine + UsageText.Text);

        if(Path.GetFileNameWithoutExtension(_programPath) != "commitfence")
            return Fail($"install must be run from the commitfence program itself, not from {_programPath}.");

        var report = _setupService.Install(OperatingSystem.IsWindows() ? _programPath.Replace('\\', '/') : _programPath, arguments.Options);
        return new SetupCommand(_output, _error).Show(report);
    }

    private ExitCode RunUpdate(string[] args)
    {
        var checkOnly = args.Length == 2 && args[1] == "--check";
        if(args.Length > 2 || (args.Length == 2 && !checkOnly))
            return Fail("update takes only --check." + Environment.NewLine + UsageText.Text);

        return new UpdateCommand(_updater, _output, _error, checkOnly).Run();
    }

    private ExitCode RunDoctor()
    {
        return new DoctorCommand(_doctor ?? throw new InvalidOperationException("Doctor is not configured."), _output).Run(_currentFolder);
    }

    private ExitCode RunInit()
    {
        return new SetupCommand(_output, _error).Show(_setupService.Init(_currentFolder));
    }

    private ExitCode RunCatalog(string[] args)
    {
        if(args.Length > 3)
            return Fail("catalog takes at most a language and an artifact type." + Environment.NewLine + UsageText.Text);

        try
        {
            return new CatalogCommand(_ruleService, _output, args.Length > 1 ? args[1] : null, args.Length > 2 ? args[2] : null).Run();
        }
        catch(RuleChangeException exception)
        {
            return Fail(exception.Message);
        }
    }

    private ExitCode Fail(string message)
    {
        _error.WriteLine(message);
        return ExitCode.BadSettingsOrUsage;
    }
}
