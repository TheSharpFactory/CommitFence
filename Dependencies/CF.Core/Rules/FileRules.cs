using CF.Core.Code;

namespace CF.Core.Rules;

/// <summary>
/// Asks the languages for the rules that the repository's own files set, for the repository that holds a settings file.
/// </summary>
internal sealed class FileRules
{
    private readonly IReadOnlyList<ILanguage> _languages;

    public FileRules(IReadOnlyList<ILanguage> languages)
    {
        _languages = languages;
    }

    public IReadOnlyList<FileRuleListing> List(string settingsPath, Strictness? strictness = null)
    {
        var repositoryFolder = Path.GetDirectoryName(settingsPath) ?? string.Empty;
        var listings = new List<FileRuleListing>();
        foreach(var source in _languages.Select(language => language.RepositoryRules).OfType<IRepositoryRules>())
            listings.Add(Read(source, repositoryFolder, strictness ?? Strictness.Standard));

        return listings;
    }

    public FileRule? Find(string settingsPath, string ruleId, Strictness? strictness = null)
    {
        return List(settingsPath, strictness).SelectMany(listing => listing.Rules).FirstOrDefault(rule => string.Equals(rule.Id, ruleId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The refusal for a change to a rule of a file, or nothing when the id is not such a rule or the file cannot be read
    /// (then the plain answer for an unknown rule stands).
    /// </summary>
    public RuleChangeException? RefusalFor(string settingsPath, string ruleId)
    {
        try
        {
            var rule = Find(settingsPath, ruleId);
            var location = rule?.Line == null ? string.Empty : $", line {rule.Line}";
            return rule == null ? null : new RuleChangeException($"{rule.Id} is set in {rule.File}{location}; the product does not change that file.");
        }
        catch(RuleChangeException)
        {
            return null;
        }
    }

    private static FileRuleListing Read(IRepositoryRules source, string repositoryFolder, Strictness strictness)
    {
        try
        {
            var listing = source.Read(repositoryFolder);
            var rules = listing.Rules.Select(rule => WithEffects(rule, strictness)).ToList();
            return new FileRuleListing(listing.LanguageId, listing.File, rules, listing.Notes);
        }
        catch(RepositoryRulesException exception)
        {
            throw new RuleChangeException(exception.Message);
        }
    }

    private static FileRule WithEffects(FileRule rule, Strictness strictness)
    {
        var main = rule.Main == null ? null : WithEffect(rule.Main, strictness);
        var exceptions = rule.Exceptions.Select(place => WithEffect(place, strictness)).ToList();
        return new FileRule(rule.Id, rule.Title, rule.File, rule.Language, main, exceptions) { IsUndescribed = rule.IsUndescribed };
    }

    private static FileRulePlace WithEffect(FileRulePlace place, Strictness strictness)
    {
        if(place.State != FileRuleState.On)
            return place;

        var effect = strictness.EffectOf(place.Setting);
        return place with { Severity = effect, State = effect == null ? FileRuleState.Off : FileRuleState.On };
    }
}