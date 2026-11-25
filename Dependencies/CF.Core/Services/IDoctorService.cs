using CF.Core.Setup;

namespace CF.Core.Services;

/// <summary>The readiness diagnosis shared by both faces.</summary>
public interface IDoctorService
{
    /// <summary>Reports whether this machine can review pushes from the repository containing the folder.</summary>
    DoctorReport Check(string folder);
}
