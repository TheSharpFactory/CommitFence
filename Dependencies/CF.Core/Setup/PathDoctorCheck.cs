using CF.Core.Ports;
using static CF.Core.Setup.DoctorTextPart;

namespace CF.Core.Setup;

/// <summary>Compares the terminal's first PATH entry with the program configured for the hook.</summary>
internal sealed class PathDoctorCheck(IDoctorGit git)
    : DoctorCheck("Git and the hook", "No other copy on the PATH", "compare the terminal's program with the hook's")
{
    protected override DoctorCheckResult Check(DoctorContext context)
    {
        var terminal = context.Machine.FindOnPath("commitfence");
        if(terminal == null)
            return Pass(new DoctorText("A terminal finds no ", CodeText("commitfence"), " on the ", CodeText("PATH"), "."));

        var configured = git.ReadHookSetting(context.Repository, HookLine.PathSetting);
        if(string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
            return new DoctorCheckResult(Group, Title, DoctorOutcome.NotChecked, new DoctorText("Not checked: it needs Git's program path to compare with the terminal's."));

        if(context.Machine.SamePath(terminal, configured))
            return Pass(new DoctorText("The ", CodeText("commitfence"), " a terminal finds is the same program."));

        var version = context.Machine.ReadVersion(terminal);
        var found = version == null
            ? new DoctorText("A terminal finds another copy: ", CodeText(terminal), ", whose version could not be read.")
            : new DoctorText("A terminal finds another copy: ", CodeText(terminal), ", version ", CodeText(version), ".");
        return Fail(found, new DoctorText("Remove the other copy's folder from the ", CodeText("PATH"), " or put the folder Git uses before it."));
    }
}
