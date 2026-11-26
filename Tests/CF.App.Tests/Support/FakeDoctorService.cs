using CF.Core.Code;
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

    public int StartedCount { get; set; } = 3;

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

    public DoctorReport Check(string folder, IProgress<int>? started = null)
    {
        lock(_syncRoot)
        {
            _folders.Add(folder);
        }

        started?.Report(StartedCount);
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
        var program = Samples.InstallFolder + @"\commitfence.exe";
        var checks = new List<DoctorCheckResult>
        {
            hookFails ? FailingHook() : Passing("Git and the hook", "The pre-push hook runs CommitFence first", new DoctorText("Line 2 of ", DoctorTextPart.CodeText(".git/hooks/pre-push"), ", ending in ", DoctorTextPart.CodeText("|| exit $?"), ", so it can stop a push.")),
            Passing("Git and the hook", "Git finds the program", new DoctorText(DoctorTextPart.CodeText("commitfence.path"), " is ", DoctorTextPart.CodeText(program), $", version {Samples.InstalledVersion}.")),
            Passing("C#", "The .NET SDK the repository needs is installed", new DoctorText("No global.json, so the newest: 11.0.100, which builds net11.0."))
        };
        var machine = new DoctorMachineFacts(program, Samples.InstalledVersion, "2.47.1.windows.1", "11.0.100", Samples.DataFolder);
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
        var fix = new DoctorFix(text, actions, DoctorExcerpt.Around("/work/invoicing/global.json", lines, 3, 4));
        var checks = report.Checks.ToList();
        checks[2] = new DoctorCheckResult("C#", "The .NET SDK the repository needs is installed", DoctorOutcome.Fails, found, fix, DoctorImpact.Sdk);
        return report with { Checks = checks, Heading = "1 check fails", Verdict = new DoctorText("Until it is fixed, a full review cannot build the push, so the push ends as Review incomplete and is refused.") };
    }

    public static DoctorReport HookSetupReport(string folder, bool added = false, bool managed = false, string? script = null, bool hasLefthookConfig = false)
    {
        var report = Report();
        const string group = "Git and the hook";
        const string title = "The pre-push hook runs CommitFence first";
        var relative = managed ? ".git/hooks/pre-push" : script ?? ".husky/pre-push";
        var path = Path.Combine(folder, relative);
        var shown = DoctorTextPart.CodeText(relative);
        var checks = report.Checks.ToList();
        if(added)
        {
            var found = new DoctorText("Line 2 of ", shown, ", ending in ", DoctorTextPart.CodeText("|| exit $?"), ", so it can stop a push.");
            checks[0] = Passing(group, title, found) with { HookPath = path };
        }
        else
        {
            var source = new List<string> { "# The tests must pass before a push", "dotnet test --nologo --verbosity quiet" };
            var before = DoctorExcerpt.Around(path, source, null);
            var after = new DoctorExcerpt(path, 2, [new FileLine(1, source[0]), new FileLine(2, CF.Core.Setup.HookLine.PrePush), new FileLine(3, source[1])]);
            var actions = new List<DoctorAction>
            {
                new DoctorAction(DoctorActionKind.OpenFile, "Open the hook script", path),
                new DoctorAction(DoctorActionKind.CopyText, "Copy the hook line", CF.Core.Setup.HookLine.PrePush)
            };
            var found = managed ? new DoctorText(LefthookAdvice.NotConfirmed) : new DoctorText("No line of ", shown, " runs CommitFence.");
            var advice = new DoctorText("Add the CommitFence line as the first command of ", shown,
                    ". The script is husky's, so CommitFence can add it for you; nothing else in the script changes.");
            var fix = managed
                ? LefthookAdvice.Fix(".git/hooks/pre-push", folder, hasLefthookConfig ? Path.Combine(folder, "lefthook.yml") : null)
                : new DoctorFix(advice, actions, before) { Addition = new HookAddition(HookKind.PrePush, relative, before, after) };
            checks[0] = new DoctorCheckResult(group, title, DoctorOutcome.Fails, found, fix, DoctorImpact.NoReview) { HookPath = managed ? null : path };
        }

        checks[1] = checks[1] with { Found = new DoctorText([.. checks[1].Found.Parts, " The repository accepts any version."]) };
        checks.Insert(2, Passing(group, "No other copy on the PATH",
            new DoctorText("The ", DoctorTextPart.CodeText("commitfence"), " a terminal finds is the same program.")));
        checks.Add(Passing("C#", "The meaning component matches the program", new DoctorText("CF.Meaning.dll beside the program is " + Samples.InstalledVersion + ".")));
        checks.Add(Passing("This repository", "The private copy is in order",
            new DoctorText("In ", DoctorTextPart.CodeText(Samples.DataFolder + @"\invoicing"), ", index last written at ", DoctorTextPart.CodeText("17:05"), ".")));
        checks.Add(Passing("This repository", ".editorconfig is found", new DoctorText("At the root, with 31 lines that set a severity.")));
        return added ? report with { Checks = checks } : report with
        {
            Checks = checks,
            Heading = "1 check fails",
            Verdict = new DoctorText("Until it is fixed, pushes from this machine are not reviewed.")
        };
    }

    private static DoctorCheckResult Passing(string group, string title, DoctorText found)
    {
        return new DoctorCheckResult(group, title, DoctorOutcome.Passes, found);
    }

    private static DoctorCheckResult FailingHook()
    {
        const string path = "/work/invoicing/.git/hooks/pre-push";
        var lines = new List<string> { "#!/bin/sh", "# Git LFS", "git lfs pre-push", HookLine };
        var moved = new List<string> { lines[0], lines[3], lines[1], lines[2] };
        var found = new DoctorText("It runs on line 4 of ", DoctorTextPart.CodeText(".git/hooks/pre-push"), ". Line 3, ", DoctorTextPart.CodeText("git lfs pre-push"), ", reads the push's ref lines before it, so CommitFence gets none.");
        var text = new DoctorText("Move the hook line up, under the first line, so it reads the ref lines first. CommitFence does not change a hook script it did not write.");
        var actions = new List<DoctorAction>
        {
            new DoctorAction(DoctorActionKind.OpenFile, "Open the hook script", path, 4),
            new DoctorAction(DoctorActionKind.CopyText, "Copy the hook line", HookLine)
        };
        var now = DoctorExcerpt.Around(path, lines, 4) with { RelatedLine = 3 };
        var fix = new DoctorFix(text, actions, now, DoctorExcerpt.Around(path, moved, 2));
        return new DoctorCheckResult("Git and the hook", "The pre-push hook runs CommitFence first", DoctorOutcome.Fails, found, fix, DoctorImpact.HookOrder);
    }
}
