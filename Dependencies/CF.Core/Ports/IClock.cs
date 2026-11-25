namespace CF.Core.Ports;

/// <summary>
/// The current time, behind a port so that nothing in the product reads the system clock directly.
/// </summary>
public interface IClock
{
    /// <summary>
    /// The current moment.
    /// </summary>
    DateTimeOffset Now { get; }

    /// <summary>Converts an instant to the clock's local calendar.</summary>
    DateTimeOffset ToLocalTime(DateTimeOffset instant)
    {
        return instant.ToOffset(Now.Offset);
    }
}