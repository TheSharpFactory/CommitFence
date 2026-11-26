using System.Security;
using CF.Core.Ports;

namespace CF.Core.Setup;

/// <summary>Shared result construction and handling of machine failures.</summary>
public abstract class DoctorCheck : IDoctorCheck
{
    /// <summary>Creates a check with its display information.</summary>
    protected DoctorCheck(string group, string title, string needsGit)
    {
        Group = group;
        Title = title;
        NeedsGit = needsGit;
    }

    /// <inheritdoc />
    public string Group { get; }

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public string NeedsGit { get; }

    /// <inheritdoc />
    public DoctorCheckResult Run(DoctorContext context)
    {
        try
        {
            return Check(context);
        }
        catch(Exception exception) when(IsMachineFailure(exception))
        {
            return Fail(new DoctorText("The check could not finish: ", Message(exception)), new DoctorText("Correct the problem shown above, then check again."));
        }
    }

    /// <summary>Runs the check with the repository facts available.</summary>
    protected abstract DoctorCheckResult Check(DoctorContext context);

    /// <summary>Builds a passing result.</summary>
    protected DoctorCheckResult Pass(DoctorText found, string? sdkVersion = null)
    {
        return new DoctorCheckResult(Group, Title, DoctorOutcome.Passes, found, SdkVersion: sdkVersion);
    }

    /// <summary>Builds a failing result with its fix.</summary>
    protected DoctorCheckResult Fail(DoctorText found, DoctorText fix, DoctorImpact impact = DoctorImpact.Review,
        IReadOnlyList<DoctorAction>? actions = null, DoctorExcerpt? excerpt = null, string? sdkVersion = null, DoctorExcerpt? afterMove = null)
    {
        return new DoctorCheckResult(Group, Title, DoctorOutcome.Fails, found, new DoctorFix(fix, actions ?? [], excerpt, afterMove), impact, sdkVersion);
    }

    /// <summary>Whether an exception is an expected machine or settings problem.</summary>
    public static bool IsMachineFailure(Exception exception)
    {
        return exception is GitException || exception is IOException || exception is UnauthorizedAccessException
            || exception is SecurityException || exception is ArgumentException
            || exception is NotSupportedException || exception is TimeoutException;
    }

    /// <summary>The error on one line.</summary>
    public static string Message(Exception exception)
    {
        return exception.Message.ReplaceLineEndings(" ").Trim();
    }
}
