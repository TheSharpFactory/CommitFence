using CF.Adapters.Machine;
using CF.Adapters.Storage;
using CF.Cli.Commands;
using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CF.Languages.CSharp;
using CF.Tests.Review.Support;

namespace CF.Tests.Toolchain.Support;

/// <summary>
/// What a command printed and the code it ended with.
/// </summary>
internal sealed record CommandResult(ExitCode ExitCode, string Output, string Error);

/// <summary>
/// The command line of the product over its real rule and review services and the real C# plug-in, in a folder of the test:
/// the way <c>commitfence rules ...</c> and <c>commitfence review --files ...</c> run, with the streams in memory and the
/// dotnet command handed in by the test.
/// </summary>
internal sealed class CommandHarness
{
    private readonly string _folder;
    private readonly ISettingsLock _settingsLock;
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;

    public CommandHarness(string folder, IProcessRunner? processRunner = null, ISettingsLock? settingsLock = null, IFileSystem? fileSystem = null)
    {
        _folder = folder;
        _fileSystem = fileSystem ?? new SystemFileSystem();
        _processRunner = processRunner ?? new FakeDotnet(folder);
        DataFolder = new TestDataFolder(Path.Combine(folder, "data"));
        _settingsLock = settingsLock ?? new FileSettingsLock(DataFolder);
    }

    public TestDataFolder DataFolder { get; }

    /// <summary>
    /// Runs the command line in the folder of the harness; the text is what standard input holds.
    /// </summary>
    public CommandResult RunWithInput(string input, params string[] arguments)
    {
        var languages = new List<ILanguage> { new CSharpLanguage(_processRunner, _fileSystem, DataFolder) };
        var settingsFile = new SettingsFile(_fileSystem, new SettingsReader(languages));
        var settings = new SettingsInTree(_fileSystem, settingsFile, "1.0.0.0");
        var namedFiles = new NamedFilesReview(new FixedClock(DateTimeOffset.UnixEpoch), languages, settings, new FileReviewer(_fileSystem, languages));
        var output = new StringWriter();
        var error = new StringWriter();
        var environment = new CommandEnvironment(_folder, _folder, Path.Combine(_folder, "commitfence"), new StringReader(input), output, error, false);
        var runner = new CommandRunner(null!, new RuleService(_fileSystem, _settingsLock, languages), new ReviewService(namedFiles, null!, null!), null!, null!, environment,
            settings: new SettingsService(_fileSystem, _settingsLock, languages),
            structure: new StructureService(_fileSystem, _settingsLock, languages, new FakeGit(_folder), new FileStructureJudgments(DataFolder, _fileSystem)),
            clock: new FixedClock(DateTimeOffset.UnixEpoch));

        var exitCode = runner.Run(arguments);

        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    public CommandResult Run(params string[] arguments)
    {
        return RunWithInput(string.Empty, arguments);
    }
}
