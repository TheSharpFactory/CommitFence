using CF.Core.Code;
using CF.Languages.CSharp.Toolchain;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// Where C# meaning comes from: the facts our analyzer writes into the compiler's log during the review's build
///.
/// </summary>
/// <remarks>
/// It answers for the tree before a push through the toolchain, and turns the answers into the meaning properties of
/// the catalog. A review applies one set of answers to many files, so the layout of the answers is made once.
/// </remarks>
internal sealed class CSharpMeaningSource : IMeaningSource
{
    private readonly CSharpToolchain _toolchain;
    private MeaningAnswers? _laidOut;
    private MeaningApplier? _applier;

    public CSharpMeaningSource(CSharpToolchain toolchain)
    {
        _toolchain = toolchain;
    }

    public MeaningAnswers Collect(MeaningRequest request)
    {
        return _toolchain.AnswerFor(request);
    }

    public IReadOnlyList<Artifact> Apply(CodeFile file, IReadOnlyList<Artifact> artifacts, MeaningAnswers answers)
    {
        if(_applier == null || !ReferenceEquals(_laidOut, answers))
        {
            _applier = new MeaningApplier(new MeaningIndex(answers));
            _laidOut = answers;
        }

        return _applier.Apply(file, artifacts);
    }
}