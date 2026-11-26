namespace CF.Core.Setup;

/// <summary>The Git hooks CommitFence can set up.</summary>
public enum HookKind
{
    /// <summary>Full review before pushing.</summary>
    PrePush,

    /// <summary>Quick review of the index before committing.</summary>
    PreCommit
}
