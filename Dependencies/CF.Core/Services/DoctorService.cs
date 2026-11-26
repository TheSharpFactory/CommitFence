using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Rules.Settings;
using CF.Core.Setup;
using static CF.Core.Setup.DoctorTextPart;

namespace CF.Core.Services;

/// <summary>Runs the checks in the order of a push, keeping each diagnosis independent.</summary>
public sealed class DoctorService : IDoctorService
{
    private readonly IClock _clock;
    private readonly IProductInfoService _product;
    private readonly IDoctorGit _git;
    private readonly IDoctorMachine _machine;
    private readonly IDataFolder _dataFolder;
    private readonly IReadOnlyList<IDoctorCheck> _checks;

    /// <summary>Creates the diagnosis with the installed languages' checks.</summary>
    public DoctorService(IClock clock, IProductInfoService product, IDoctorGit git, IDoctorMachine machine, IDataFolder dataFolder,
        IFileSystem files, ISnapshotInspector snapshots, SettingsFile settings, IReadOnlyList<ILanguage> languages)
    {
        _clock = clock;
        _product = product;
        _git = git;
        _machine = machine;
        _dataFolder = dataFolder;
        _checks = [
            new PrePushDoctorCheck(git, files), new HookProgramDoctorCheck(git, files, settings), new PathDoctorCheck(git),
            .. languages.SelectMany(language => language.DoctorChecks),
            new PrivateCopyDoctorCheck(snapshots), new EditorConfigDoctorCheck(files)
        ];
    }

    /// <inheritdoc />
    public DoctorReport Check(string folder, IProgress<int>? started = null)
    {
        var startTime = _clock.Now;
        var version = ProductVersion.TryParseOwn(_product.Version)?.ToString() ?? _product.Version;
        var program = _machine.ProgramPath;
        var checks = Run(folder, program, version, started, out var gitVersion);
        var machine = new DoctorMachineFacts(program, version, gitVersion, checks.Select(check => check.SdkVersion).FirstOrDefault(sdk => sdk != null), ReadDataFolder());
        var checkedAt = _clock.Now;
        var failed = checks.Where(check => check.Outcome == DoctorOutcome.Fails).ToList();
        var beforeCommit = checks.Exists(check => check.Title == PrePushDoctorCheck.PreCommitTitle);
        var heading = failed.Count switch
        {
            0 when checks.All(check => check.Outcome == DoctorOutcome.Passes) => beforeCommit ? "Ready for commits" : "Ready for pushes",
            0 => "Checks could not finish",
            1 => "1 check fails",
            _ => $"{failed.Count} checks fail"
        };
        return new DoctorReport(checks, machine, checkedAt, checkedAt >= startTime ? checkedAt - startTime : TimeSpan.Zero, heading, Verdict(failed, checks.All(check => check.Outcome == DoctorOutcome.Passes), beforeCommit));
    }

    private string? ReadDataFolder()
    {
        try
        {
            return _dataFolder.RootFolder;
        }
        catch(Exception exception) when(DoctorCheck.IsMachineFailure(exception))
        {
            // The private copy check reports the cause; the machine panel can only leave the path unknown.
            return null;
        }
    }

    private List<DoctorCheckResult> Run(string folder, string program, string version, IProgress<int>? started, out string? gitVersion)
    {
        gitVersion = null;
        RepositoryLocation? repository;
        try
        {
            gitVersion = _git.ReadVersion();
            if(gitVersion == null)
            {
                return CannotStart(new DoctorText(CodeText("git"), " was not found on the ", CodeText("PATH"), "."),
                    new DoctorText("Install Git or add it to the ", CodeText("PATH"), ", then check again."), true);
            }

            repository = _git.FindRepository(folder);
            if(repository == null)
            {
                return CannotStart(new DoctorText(CodeText(folder), " is not in a Git repository."),
                    new DoctorText("Run Doctor from a folder inside the repository."), false);
            }
        }
        catch(Exception exception) when(DoctorCheck.IsMachineFailure(exception))
        {
            return CannotStart(new DoctorText("Git could not locate the repository: ", DoctorCheck.Message(exception)),
                new DoctorText("Correct the Git problem shown above, then check again."), gitVersion == null);
        }

        var context = new DoctorContext(repository, program, version, _machine, _git);
        var results = new List<DoctorCheckResult>();
        started?.Report(1);
        foreach(var check in _checks)
        {
            results.Add(check.Run(context));
            started?.Report(results.Count + 1);
        }

        return results;
    }

    private List<DoctorCheckResult> CannotStart(DoctorText found, DoctorText fix, bool needsGit)
    {
        var first = _checks[0];
        var results = new List<DoctorCheckResult>
        {
            new DoctorCheckResult(first.Group, first.Title, DoctorOutcome.Fails, found, new DoctorFix(fix, []))
        };
        foreach(var check in _checks.Skip(1))
        {
            var reason = needsGit ? new DoctorText("Not checked: it needs ", CodeText("git"), " to ", check.NeedsGit, ".")
                : new DoctorText("Not checked: it needs a Git repository.");
            results.Add(new DoctorCheckResult(check.Group, check.Title, DoctorOutcome.NotChecked, reason));
        }

        return results;
    }

    private static DoctorText Verdict(List<DoctorCheckResult> failed, bool ready, bool beforeCommit)
    {
        if(beforeCommit)
        {
            return ready ? new DoctorText("Every check passes. Staged changes are reviewed before they are committed.")
                : new DoctorText("Check the results below to see whether this machine can review staged changes before a commit.");
        }

        if(failed.Count == 0)
        {
            return ready ? new DoctorText("Every check passes. A push from this machine is reviewed before it leaves.")
                : new DoctorText("Some checks did not run. Whether a push will be reviewed is unknown.");
        }

        return failed.Max(check => check.Impact) switch
        {
            DoctorImpact.NoReview => new DoctorText("Until it is fixed, pushes from this machine are not reviewed."),
            DoctorImpact.HookDoesNotStopPush => new DoctorText("Until it is fixed, a review that refuses a push may not stop it."),
            DoctorImpact.HookOrder => new DoctorText("Until it is fixed, a push that sends a commit ends as Review incomplete and is refused."),
            DoctorImpact.HookOrderUncertain => new DoctorText("Until it is fixed, a push that sends a commit can end as Review incomplete and be refused."),
            DoctorImpact.Program => new DoctorText("Until it is fixed, a push ends as Review incomplete and is refused."),
            DoctorImpact.Sdk => new DoctorText("Until it is fixed, a full review cannot build the push, so the push ends as Review incomplete and is refused."),
            DoctorImpact.Review => new DoctorText("A review may not finish until it is fixed."),
            _ => throw new ArgumentOutOfRangeException(nameof(failed))
        };
    }
}
