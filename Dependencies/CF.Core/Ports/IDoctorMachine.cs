namespace CF.Core.Ports;

/// <summary>Read-only inspection of programs for Doctor.</summary>
public interface IDoctorMachine
{
    /// <summary>The path of the command-line program in the current installation.</summary>
    string ProgramPath { get; }

    /// <summary>The first executable with this name on the current PATH, or nothing.</summary>
    string? FindOnPath(string name);

    /// <summary>Whether the paths name the same file after resolving links.</summary>
    bool SamePath(string left, string right);

    /// <summary>Reads a version from a file without starting or loading it.</summary>
    string? ReadVersion(string path);

    /// <summary>Whether Git can run this file as a hook.</summary>
    bool IsExecutable(string path);
}
