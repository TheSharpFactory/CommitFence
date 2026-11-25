namespace CF.Core.Setup;

/// <summary>One read-only diagnosis for Doctor.</summary>
public interface IDoctorCheck
{
    /// <summary>The heading above the check.</summary>
    string Group { get; }

    /// <summary>The check's title.</summary>
    string Title { get; }

    /// <summary>Why the check needs Git.</summary>
    string NeedsGit { get; }

    /// <summary>Runs the check once.</summary>
    DoctorCheckResult Run(DoctorContext context);
}
