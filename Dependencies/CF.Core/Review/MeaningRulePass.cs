using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;

namespace CF.Core.Review;

/// <summary>
/// Applies the rules that need meaning, once the build has answered.
/// </summary>
/// <remarks>
/// When a language has no complete answers, each of its rules that needs meaning is reported as not run, which makes
/// the review incomplete: never silently clean.
/// </remarks>
internal static class MeaningRulePass
{
    public static void Run(IReadOnlyList<RuleDefinition> rules, IReadOnlyList<ReadSource> sources, IReadOnlyList<LanguageAnswers> answers, IReadOnlyList<ILanguage> languages, ReviewRun run)
    {
        foreach(var language in languages)
        {
            var languageRules = FindRules(rules, language);
            if(languageRules.Count > 0)
                RunLanguage(language, languageRules, sources, answers, run);
        }
    }

    public static void ReportNotRun(IReadOnlyList<RuleDefinition> rules, IReadOnlyList<ILanguage> languages, string reason, ReviewRun run)
    {
        foreach(var language in languages)
        {
            foreach(var rule in FindRules(rules, language))
                run.AddNotRun(rule.Id, reason);
        }
    }

    private static List<RuleDefinition> FindRules(IReadOnlyList<RuleDefinition> rules, ILanguage language)
    {
        return rules.Where(rule => rule.Language == language.Id && MeaningNeeds.Has(rule, language)).ToList();
    }

    private static void RunLanguage(ILanguage language, List<RuleDefinition> rules, IReadOnlyList<ReadSource> sources, IReadOnlyList<LanguageAnswers> answers, ReviewRun run)
    {
        var found = answers.FirstOrDefault(candidate => candidate.Language == language)?.Answers;
        var problem = FindProblem(language, found);
        if(problem != null)
        {
            foreach(var rule in rules)
                run.AddNotRun(rule.Id, problem);

            return;
        }

        foreach(var source in sources.Where(candidate => candidate.Language == language))
            ApplyRules(language, rules, source, found!, run);
    }

    private static string? FindProblem(ILanguage language, MeaningAnswers? answers)
    {
        if(language.Meaning == null)
            return $"the {language.Name} plug-in has no source of meaning";

        if(answers == null)
            return "the build did not run, so nothing is known about what the code refers to";

        if(!answers.IsComplete)
            return "the build did not answer completely: " + answers.Gaps[0];

        return null;
    }

    private static void ApplyRules(ILanguage language, List<RuleDefinition> rules, ReadSource source, MeaningAnswers answers, ReviewRun run)
    {
        var findings = new List<Finding>();
        foreach(var way in source.Ways)
        {
            var artifacts = language.Meaning!.Apply(source.File, way.Artifacts, answers);
            foreach(var rule in rules)
                findings.AddRange(RuleEvaluator.Evaluate(rule, artifacts));
        }

        run.AddFindings(ReachFilter.Keep(ReportOnce(findings), source.Change));
    }

    /// <summary>
    /// A file compiled in several ways is judged in each, and most of its code is the same in all: a finding is
    /// reported once.
    /// </summary>
    private static List<Finding> ReportOnce(List<Finding> findings)
    {
        return findings.GroupBy(finding => (finding.RuleId, finding.Line, finding.Column, finding.Message)).Select(group => group.First()).ToList();
    }
}