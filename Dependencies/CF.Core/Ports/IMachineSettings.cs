namespace CF.Core.Ports;

/// <summary>
/// What the app keeps about itself on this machine, apart from any repository: where it keeps its data, and the one switch that
/// asks it to move less on a system that has no setting of its own.
/// </summary>
public interface IMachineSettings
{
    /// <summary>
    /// The folder the product keeps what is local to this machine in.
    /// </summary>
    string DataFolder { get; }

    /// <summary>
    /// Whether the person turned "Reduce motion" on. False when they never did or the file cannot be read.
    /// </summary>
    bool ReduceMotion { get; }

    /// <summary>
    /// Keeps the switch. A file that cannot be written is not an error: the switch holds until the app ends and is off at the next start.
    /// </summary>
    void SaveReduceMotion(bool reduce);
}
