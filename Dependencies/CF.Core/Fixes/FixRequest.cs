using CF.Core.Findings;
using CF.Core.Ports;

namespace CF.Core.Fixes;

/// <summary>The finding and the exact repository tree that was reviewed.</summary>
public sealed record FixRequest(RepositoryLocation Repository, string Tree, Finding Finding);
