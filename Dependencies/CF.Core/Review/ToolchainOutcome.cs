using CF.Core.Code;

namespace CF.Core.Review;

/// <summary>
/// What one language's toolchain answered for a change set.
/// </summary>
internal sealed class ToolchainOutcome
{
    public ToolchainOutcome(ToolchainWork work, ToolchainResult result)
    {
        Work = work;
        Result = result;
    }

    public ToolchainWork Work { get; }

    public ToolchainResult Result { get; }
}