namespace CF.Core.Ports;

/// <summary>
/// The user's own git settings (the global file), behind a port so that a check can point them at a scratch file and
/// Core starts no process.
/// </summary>
public interface IUserGitSettings
{
    /// <summary>
    /// The value of the setting, or nothing when it is not set.
    /// </summary>
    string? Read(string key);

    /// <summary>
    /// Sets the value, replacing the one that was there. Throws a <see cref="GitException" /> when git cannot write it.
    /// </summary>
    void Write(string key, string value);
}