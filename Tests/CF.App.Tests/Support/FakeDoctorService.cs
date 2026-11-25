using CF.Core.Services;
using CF.Core.Setup;

namespace CF.App.Tests.Support;

/// <summary>
/// A diagnosis that answers from what the test sets and records every folder it was asked about. The answer is made on the
/// thread that runs the check, so a test can hold it there until it lets it go.
/// </summary>
internal sealed class FakeDoctorService : IDoctorService
{
    private readonly Lock _syncRoot = new Lock();
    private readonly List<string> _folders = new List<string>();

    public Func<string, DoctorReport> Answer { get; set; } = _ => Report();

    public IReadOnlyList<string> Folders
    {
        get
        {
            lock(_syncRoot)
            {
                return _folders.ToList();
            }
        }
    }

    public DoctorReport Check(string folder)
    {
        lock(_syncRoot)
        {
            _folders.Add(folder);
        }

        return Answer(folder);
    }

    /// <summary>
    /// The hook line the fix of the failing check copies.
    /// </summary>
    public static string HookLine
    {
        get { return "\"$(git config --get commitfence.path)\" review --hook pre-push \"$@\" || exit $?"; }
    }

    /// <summary>
    /// The checks of the plan's first group and the C# group; the first one fails (the hook line is not first) when
    /// <paramref name="hookFails"/> is set.
    /// </summary>
    public static DoctorReport Report(bool hookFails = false, string? heading = null)
    {
        var checks = new List<DoctorCheckResult>
        {
            hookFails ? FailingHook() : Passing("Git and the hook", "The pre-push hook runs CommitFence first", new DoctorText("Line 2 of ", DoctorTextPart.CodeText(".git/hooks/pre-push"), ", ending in ", DoctorTextPart.CodeText("|| exit $?"), ", so it can stop a push.")),
            Passing("Git and the hook", "Git finds the program", new DoctorText(DoctorTextPart.CodeText("commitfence.path"), " is ", DoctorTextPart.CodeText("C:\\Tools\\CommitFence\\commitfence.exe"), ", version 1.0.0.0.")),
            Passing("C#", "The .NET SDK the repository needs is installed", new DoctorText("No global.json, so the newest: 11.0.100, which builds net11.0."))
        };
        var machine = new DoctorMachineFacts("C:\\Tools\\CommitFence\\commitfence.exe", "1.0.0.0", "2.47.1.windows.1", "11.0.100", "C:\\Users\\dev\\AppData\\Local\\CommitFence");
        var verdict = hookFails ? new DoctorText("Until it is fixed, a push that sends a commit ends as Review incomplete and is refused.") : new DoctorText("Every check passes. A push from this machine is reviewed before it leaves.");
        return new DoctorReport(checks, machine, Samples.Now, TimeSpan.FromSeconds(1.3), heading ?? (hookFails ? "1 check fails" : "Ready for pushes"), verdict);
    }

    /// <summary>
    /// The report when the .NET SDK that global.json asks for is missing: the C# check fails, the hook check above it passes.
    /// </summary>
    public static DoctorReport SdkReport()
    {
        var report = Report();
        var lines = new List<string> { "{", "  \"sdk\": {", "    \"version\": \"10.0.100\",", "    \"rollForward\": \"disable\"", "  }", "}" };
        var found = new DoctorText(DoctorTextPart.CodeText("global.json"), " asks for ", DoctorTextPart.CodeText("10.0.100"), " and does not roll forward; this machine has ", DoctorTextPart.CodeText("11.0.100"), " only.");
        var text = new DoctorText("Install the .NET SDK 10.0.100, or let ", DoctorTextPart.CodeText("global.json"), " roll forward to a newer one. CommitFence does not change ", DoctorTextPart.CodeText("global.json"), ".");
        var actions = new List<DoctorAction>
        {
            new DoctorAction(DoctorActionKind.OpenFile, "Open global.json", "/work/invoicing/global.json", 3),
            new DoctorAction(DoctorActionKind.OpenWeb, "Open the .NET download page", "https://dotnet.microsoft.com/download")
        };
        var fix = new DoctorFix(text, actions, DoctorExcerpt.Around("/work/invoicing/global.json", lines, 3));
        var checks = report.Checks.ToList();
        checks[2] = new DoctorCheckResult("C#", "The .NET SDK the repository needs is installed", DoctorOutcome.Fails, found, fix, DoctorImpact.Sdk);
        return report with { Checks = checks, Heading = "1 check fails", Verdict = new DoctorText("Until it is fixed, a full review cannot build the push, so the push ends as Review incomplete and is refused.") };
    }

    private static DoctorCheckResult Passing(string group, string title, DoctorText found)
    {
        return new DoctorCheckResult(group, title, DoctorOutcome.Passes, found);
    }

    private static DoctorCheckResult FailingHook()
    {
        var lines = new List<string> { "#!/bin/sh", "command -v git-lfs >/dev/null 2>&1 || { echo >&2 \"git-lfs was not found\"; exit 2; }", "git lfs pre-push \"$@\"", HookLine };
        var found = new DoctorText("It runs on line 4 of ", DoctorTextPart.CodeText(".git/hooks/pre-push"), ". Line 3, ", DoctorTextPart.CodeText("git lfs pre-push"), ", reads the push's ref lines before it, so CommitFence gets none.");
        var text = new DoctorText("Move the hook line up, under the first line, so it reads the ref lines first. CommitFence does not change a hook script it did not write.");
        var actions = new List<DoctorAction>
        {
            new DoctorAction(DoctorActionKind.OpenFile, "Open the hook script", "/work/invoicing/.git/hooks/pre-push", 4),
            new DoctorAction(DoctorActionKind.CopyText, "Copy the hook line", HookLine)
        };
        var fix = new DoctorFix(text, actions, DoctorExcerpt.Around("/work/invoicing/.git/hooks/pre-push", lines, 4));
        return new DoctorCheckResult("Git and the hook", "The pre-push hook runs CommitFence first", DoctorOutcome.Fails, found, fix, DoctorImpact.HookOrder);
    }
}
