namespace CF.Core.Ports;

/// <summary>The state read from the private checkout and its index.</summary>
/// <param name="State">How the checkout and index agree.</param>
/// <param name="Folder">The repository's data folder.</param>
/// <param name="IndexUpdatedAt">When the index file was last written, if known.</param>
public sealed record PrivateCopyStatus(PrivateCopyState State, string Folder, DateTimeOffset? IndexUpdatedAt = null);
