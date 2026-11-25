using System.Runtime.InteropServices;

namespace CF.App.Parts;

/// <summary>
/// Whether the system asks for less motion. When it does, every length of the design's motion is 0 and the busy ring does not turn.
/// Windows says so itself; elsewhere the app keeps one switch (read by <see cref="Reduced"/>) that Settings sets.
/// </summary>
internal static partial class Motion
{
    private const uint _getClientAreaAnimation = 0x1042;

    /// <summary>
    /// Where the answer comes from. The tests give their own.
    /// </summary>
    public static Func<bool> Reduced { get; set; } = SystemReducesMotion;

    /// <summary>
    /// The one switch kept on this machine for systems that have none of their own.
    /// </summary>
    public static bool ReducedOnThisMachine { get; set; }

    public static bool IsReduced
    {
        get
        {
            return Reduced();
        }
    }

    private static bool SystemReducesMotion()
    {
        if(!OperatingSystem.IsWindows())
            return ReducedOnThisMachine;

        var enabled = 1;
        return SystemParametersInfo(_getClientAreaAnimation, 0, ref enabled, 0) && enabled == 0;
    }

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint parameter, ref int value, uint flags);
}
