namespace CF.App.Doctor;

/// <summary>
/// What the verdict of the Doctor screen says: the checks run, every one passes, one or more fail, or some did not run and
/// none failed.
/// </summary>
internal enum DoctorVerdictKind
{
    Checking,
    Ready,
    Fails,
    LeftToYou,
    Unfinished
}
