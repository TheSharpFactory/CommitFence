namespace CF.Core.Setup;

/// <summary>What a Doctor check found.</summary>
public enum DoctorOutcome
{
    /// <summary>The check passed.</summary>
    Passes,

    /// <summary>The check found a problem.</summary>
    Fails,

    /// <summary>A fact needed for the check was unavailable.</summary>
    NotChecked
}
