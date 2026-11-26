using CF.Core.Changes;
using CF.Core.Code;

namespace CF.Core.Review;

/// <summary>
/// A changed source file already read by its language's reader, in every way the build compiles it: kept so that the
/// rules that need meaning can be applied to the same artifacts once the build has answered.
/// </summary>
internal sealed class ReadSource
{
    public ReadSource(ILanguage language, CodeFile file, IReadOnlyList<ReadWay> ways, ChangedFile change)
    {
        Language = language;
        File = file;
        Ways = ways;
        Change = change;
    }

    public ILanguage Language { get; }

    public CodeFile File { get; }

    public IReadOnlyList<ReadWay> Ways { get; }

    public ChangedFile Change { get; }
}