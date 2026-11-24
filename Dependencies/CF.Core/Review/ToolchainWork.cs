using CF.Core.Changes;
using CF.Core.Code;

namespace CF.Core.Review;

/// <summary>
/// What one language's toolchain has to look at in a change: the changed files it concerns.
/// </summary>
internal sealed class ToolchainWork
{
    public ToolchainWork(ILanguage language, IToolchain toolchain, IReadOnlyList<ChangedFile> files)
    {
        Language = language;
        Toolchain = toolchain;
        Files = files;
    }

    public ILanguage Language { get; }

    public IToolchain Toolchain { get; }

    public IReadOnlyList<ChangedFile> Files { get; }
}