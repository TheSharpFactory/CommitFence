namespace CF.Core.Ports;

/// <summary>
/// When this program last asked for a newer release, so that it asks once a day.
/// </summary>
public interface IUpdateCheckRecord
{
    /// <summary>
    /// The time of the last check, or null when there was none or the record cannot be read.
    /// </summary>
    DateTime? LastCheckUtc { get; }

    /// <summary>
    /// Keeps the time of a check. A record that cannot be written is not an error: the next start asks again.
    /// </summary>
    void Save(DateTime checkedUtc);
}