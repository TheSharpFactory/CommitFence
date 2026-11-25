using System.Globalization;
using CF.Core.Ports;
using static CF.Core.Setup.DoctorTextPart;

namespace CF.Core.Setup;

/// <summary>Checks the repository's private checkout without moving it.</summary>
internal sealed class PrivateCopyDoctorCheck(ISnapshotInspector snapshots)
    : DoctorCheck("This repository", "The private copy is in order", "locate the private copy")
{
    protected override DoctorCheckResult Check(DoctorContext context)
    {
        var status = snapshots.Inspect(context.Repository);
        var folder = CodeText(status.Folder);
        return status.State switch
        {
            PrivateCopyState.Absent => Pass(new DoctorText("No private copy yet. The first full review creates it in ", folder, ".")),
            PrivateCopyState.InUse => Fail(new DoctorText("A review is using the private copy in ", folder, "."),
                new DoctorText("When that review finishes, check again.")),
            PrivateCopyState.IndexLocked => IndexLocked(status.Folder),
            PrivateCopyState.OutOfOrder => Fail(new DoctorText("The private copy in ", folder, " and its index are incomplete or disagree."),
                new DoctorText("Run a full review to let CommitFence restore the private copy, then check again.")),
            PrivateCopyState.InOrder => Pass(new DoctorText("In ", folder, ", index last written at ",
                CodeText(status.IndexUpdatedAt?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "an unknown time"), ".")),
            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };
    }

    private DoctorCheckResult IndexLocked(string folder)
    {
        var path = Path.Combine(folder, "index.lock");
        return Fail(new DoctorText("The private copy's index is locked by a review that stopped."),
            new DoctorText("Once no review is running, delete ", CodeText(path), ", then check again."),
            actions: [new DoctorAction(DoctorActionKind.CopyText, "Copy the lock file path", path)]);
    }
}
