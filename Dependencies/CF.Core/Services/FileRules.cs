using CF.Core.Code;
using CF.Core.Rules;

namespace CF.Core.Services;

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

    public IReadOnlyList<FileRuleListing> List(string settingsPath)
    {
        var repositoryFolder = Path.GetDirectoryName(settingsPath) ?? string.Empty;
        var listings = new List<FileRuleListing>();
        foreach(var source in _languages.Select(language => language.RepositoryRules).OfType<IRepositoryRules>())
            listings.Add(Read(source, repositoryFolder));

        return listings;
    }

    public FileRule? Find(string settingsPath, string ruleId)
    {
        return List(settingsPath).SelectMany(listing => listing.Rules).FirstOrDefault(rule => string.Equals(rule.Id, ruleId, StringComparison.OrdinalIgnoreCase));
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

    private static FileRuleListing Read(IRepositoryRules source, string repositoryFolder)
    {
        try
        {
            return source.Read(repositoryFolder);
        }
        catch(RepositoryRulesException exception)
        {
            throw new RuleChangeException(exception.Message);
        }
    }
}