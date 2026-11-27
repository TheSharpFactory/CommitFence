using System.Text.RegularExpressions;

namespace CF.Core.Review;

/// <summary>
/// The address of a remote as a report may name it: where the code is hosted, never who signed in to it and never a folder
/// of the machine that reviewed.
/// </summary>
public static partial class RemoteAddress
{
    private static readonly string[] _hostedSchemes = ["https", "http", "ssh", "git"];

    /// <summary>
    /// The address without credentials, query or fragment, in the form scheme://host/path; the form user@host:path becomes
    /// ssh://host/path. Empty when the address is a path of a machine, which says nothing to anyone else, or cannot be read.
    /// </summary>
    public static string PublicOf(string? address)
    {
        var text = address?.Trim() ?? string.Empty;
        if(text.Length == 0)
            return string.Empty;

        var scp = ScpForm().Match(text);
        if(scp.Success)
            return $"ssh://{scp.Groups["host"].Value}/{scp.Groups["path"].Value.TrimStart('/')}";

        if(!Uri.TryCreate(text, UriKind.Absolute, out var uri) || !_hostedSchemes.Contains(uri.Scheme) || uri.Host.Length == 0)
            return string.Empty;

        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port;
        return $"{uri.Scheme}://{uri.Host}{port}{uri.AbsolutePath}";
    }

    // user@host:path, which git reads as ssh; a single letter before the colon is a drive, not a host
    [GeneratedRegex(@"^(?:[^@/\\s]+@)?(?<host>[^@/\\s:]{2,}):(?!//)(?<path>[^\\s][^\s]*)$")]
    private static partial Regex ScpForm();
}
