using CF.Core.Ports;

namespace CF.Tests.Rules.Support;

/// <summary>
/// A settings lock that lets a test choose who holds it when: it announces the moment its owner asks for the lock, and
/// after it has the lock it does not let the owner go on until the test says so. The lock itself is the product's.
/// </summary>
internal sealed class GatedLock : ISettingsLock
{
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(60);

    private readonly ISettingsLock _inner;
    private readonly bool _holdsUntilReleased;

    public GatedLock(ISettingsLock inner, bool holdsUntilReleased)
    {
        _inner = inner;
        _holdsUntilReleased = holdsUntilReleased;
    }

    /// <summary>
    /// Set when the owner has asked for the lock.
    /// </summary>
    public ManualResetEventSlim Requested { get; } = new ManualResetEventSlim();

    /// <summary>
    /// Set when the owner has the lock.
    /// </summary>
    public ManualResetEventSlim Held { get; } = new ManualResetEventSlim();

    /// <summary>
    /// Set by the test to let the owner go on once it holds the lock.
    /// </summary>
    public ManualResetEventSlim Release { get; } = new ManualResetEventSlim();

    public IDisposable Take(string settingsPath)
    {
        Requested.Set();
        var held = _inner.Take(settingsPath);
        Held.Set();
        if(_holdsUntilReleased && !Release.Wait(_guard))
            throw new TimeoutException("the test never released the lock");

        return held;
    }
}
