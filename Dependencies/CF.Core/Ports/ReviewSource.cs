namespace CF.Core.Ports;

/// <summary>The entry point that started a push review.</summary>
public enum ReviewSource
{
    /// <summary>The Git pre-push hook.</summary>
    Hook,

    /// <summary>A command typed at the terminal.</summary>
    CommandLine,

    /// <summary>The desktop Review screen.</summary>
    App
}
