namespace CF.Core.Code;

/// <summary>How a source file exempts a rule.</summary>
public enum ExemptionForm
{
    /// <summary>A CommitFence allow comment.</summary>
    AllowComment,

    /// <summary>A compiler directive disabling a diagnostic.</summary>
    Pragma,

    /// <summary>A diagnostic suppression attribute.</summary>
    SuppressMessage
}