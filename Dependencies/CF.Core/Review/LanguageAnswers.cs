using CF.Core.Code;

namespace CF.Core.Review;

/// <summary>
/// What one language's build said about the tree under review.
/// </summary>
internal sealed class LanguageAnswers
{
    public LanguageAnswers(ILanguage language, MeaningAnswers answers)
    {
        Language = language;
        Answers = answers;
    }

    public ILanguage Language { get; }

    public MeaningAnswers Answers { get; }
}