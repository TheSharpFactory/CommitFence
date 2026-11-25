using CF.Core.Ports;

namespace CF.Adapters.Machine;

/// <summary>
/// The clock of the machine the program runs on.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset Now
    {
        get { return DateTimeOffset.Now; }
    }

    /// <inheritdoc />
    public DateTimeOffset ToLocalTime(DateTimeOffset instant)
    {
        return instant.ToLocalTime();
    }
}