namespace CF.Core.Ports;

/// <summary>The readable records, the number of lines left out and any error that stopped the read.</summary>
public sealed record ReviewHistoryRead(IReadOnlyList<ReviewRecord> Records, int UnreadableLines, string? ReadError = null);
