namespace CF.Core.Updates;

/// <summary>
/// The latest release on GitHub, and the download for this machine.
/// </summary>
public sealed class ReleaseInfo
{
    /// <summary>
    /// The version the release's tag names.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// The release text (the version's CHANGELOG section, Markdown).
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// The release on GitHub, opened in the browser when the program cannot update itself.
    /// </summary>
    public Uri? PageUri { get; init; }

    /// <summary>
    /// The download for this OS and processor and this kind of folder: "CommitFence-win-x64.zip", or
    /// "commitfence-cli-win-x64.zip" where only the command line is.
    /// </summary>
    public string? PackageName { get; init; }

    /// <summary>
    /// Null when the release has no download for this machine.
    /// </summary>
    public Uri? PackageUri { get; init; }

    /// <summary>
    /// SHA256SUMS.txt: the SHA-256 of every download. Null when the release has none.
    /// </summary>
    public Uri? ChecksumsUri { get; init; }

    /// <summary>
    /// SHA256SUMS.txt.sig: SHA256SUMS.txt signed with CommitFence's key. Null when the release has none.
    /// </summary>
    public Uri? SignatureUri { get; init; }

    /// <summary>
    /// The program can download and check this release itself.
    /// </summary>
    public bool CanInstall
    {
        get { return PackageUri != null && ChecksumsUri != null && SignatureUri != null; }
    }
}
