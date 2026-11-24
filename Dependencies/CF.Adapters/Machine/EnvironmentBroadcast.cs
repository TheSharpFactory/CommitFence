using System.Runtime.InteropServices;

namespace CF.Adapters.Machine;

/// <summary>
/// Tells Windows that the user's environment changed, so that programs started afterwards from the desktop see the new
/// PATH without signing out.
/// </summary>
internal static partial class EnvironmentBroadcast
{
    private const nint _everyWindow = 0xFFFF;
    private const uint _settingChangeMessage = 0x001A;
    private const uint _abortIfHung = 0x0002;
    private const uint _timeoutMilliseconds = 5000;

    public static void Announce()
    {
        SendMessageTimeout(_everyWindow, _settingChangeMessage, 0, "Environment", _abortIfHung, _timeoutMilliseconds, out _);
    }

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint SendMessageTimeout(nint window, uint message, nint wParam, string lParam, uint flags, uint timeoutMilliseconds, out nint result);
}