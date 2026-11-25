namespace CF.Core.Setup;

/// <summary>The consequences of a failed check, in order of priority.</summary>
public enum DoctorImpact
{
    /// <summary>A review may be affected.</summary>
    Review,

    /// <summary>The review cannot build the push.</summary>
    Sdk,

    /// <summary>The hook cannot start a suitable program.</summary>
    Program,

    /// <summary>An earlier hook command may consume the input.</summary>
    HookOrderUncertain,

    /// <summary>An earlier hook command reads the push input.</summary>
    HookOrder,

    /// <summary>A refusal by the review may be ignored by the hook.</summary>
    HookDoesNotStopPush,

    /// <summary>The hook does not run a review.</summary>
    NoReview
}
