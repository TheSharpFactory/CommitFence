using CF.Core.Ports;

namespace CF.Core.Setup;

/// <summary>Facts available to a check in a Git repository.</summary>
/// <param name="Repository">The repository to diagnose.</param>
/// <param name="ProgramPath">The command-line program in the current installation.</param>
/// <param name="ProgramVersion">The running product's version.</param>
/// <param name="Machine">Read-only queries about the machine.</param>
/// <param name="Git">Read-only queries about the repository's Git configuration.</param>
public sealed record DoctorContext(RepositoryLocation Repository, string ProgramPath, string ProgramVersion, IDoctorMachine Machine, IDoctorGit Git);
