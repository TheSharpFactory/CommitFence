namespace CF.Core.Ports;

/// <summary>
/// The user's own PATH, the one kept for the user and not for the machine, behind a port so that a check can redirect
/// it.
/// </summary>
public interface IUserPath
{
    /// <summary>
    /// Whether the product can change the user's PATH on this system. It cannot on systems where the PATH is set by a
    /// shell profile.
    /// </summary>
    bool CanChange { get; }

    /// <summary>
    /// Whether the folder is already one of the entries.
    /// </summary>
    bool Contains(string folder);

    /// <summary>
    /// Adds the folder after the entries that are there. Throws an IOException or an UnauthorizedAccessException when
    /// it cannot be written.
    /// </summary>
    void Add(string folder);
}