using CF.Core.Setup;

namespace CF.Core.Services;

/// <summary>The readiness diagnosis shared by both faces.</summary>
public interface IDoctorService
{
    /// <summary>Reports whether this machine can review pushes from the repository containing the folder.</summary>
    /// <param name="folder">A folder in the repository.</param>
    /// <param name="started">Receives the 1-based position of the active check, then one past the last check when all are finished.</param>
    DoctorReport Check(string folder, IProgress<int>? started = null);
}
