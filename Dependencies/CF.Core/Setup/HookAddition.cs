namespace CF.Core.Setup;

/// <summary>The hook, its original lines, and the insertion that Doctor showed.</summary>
public sealed record HookAddition(HookKind Kind, string ShownPath, DoctorExcerpt Before, DoctorExcerpt After);
