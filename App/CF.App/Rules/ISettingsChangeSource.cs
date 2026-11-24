namespace CF.App.Rules;

/// <summary>
/// Says when a settings file changed on disk. The list reloads when it does; a test raises the event itself.
/// </summary>
internal interface ISettingsChangeSource : IDisposable
{
    /// <summary>
    /// Raised on the interface's thread, shortly after the file stopped changing.
    /// </summary>
    event Action? Changed;
}
