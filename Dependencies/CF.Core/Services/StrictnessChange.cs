using CF.Core.Rules;

namespace CF.Core.Services;

/// <summary>The mappings and file revisions before and after a strictness change.</summary>
public sealed record StrictnessChange(Strictness Before, Strictness After, string PreviousRevision, string Revision);
