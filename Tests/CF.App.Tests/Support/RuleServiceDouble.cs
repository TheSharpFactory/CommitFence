using CF.Adapters.Machine;
using CF.Adapters.Storage;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;
using CF.Core.Services;
using CF.Languages.CSharp;

namespace CF.App.Tests.Support;

/// <summary>
/// The real rule service over settings files in a temporary folder, with one difference: the rules of .editorconfig are what the
/// test says, and the listing can be held at a gate (the real one starts the SDK's tool to find the titles).
/// </summary>
internal sealed class RuleServiceDouble : IRuleService
{
    private readonly RuleService _inner;
    private readonly Lock _syncRoot = new Lock();
    private int _fileListings;

    public RuleServiceDouble(string dataFolder)
    {
        var fileSystem = new SystemFileSystem();
        var dataFolderPort = new FixedDataFolder(dataFolder);
        var languages = new List<ILanguage> { new CSharpLanguage(new NoProcessRunner(), fileSystem, dataFolderPort) };
        _inner = new RuleService(fileSystem, new FileSettingsLock(dataFolderPort), languages);
    }

    /// <summary>
    /// What the listing of .editorconfig answers.
    /// </summary>
    public IReadOnlyList<FileRuleListing> FileListings { get; set; } = new List<FileRuleListing>();

    /// <summary>
    /// When set, the listing of .editorconfig answers with this refusal.
    /// </summary>
    public string? FileListingRefusal { get; set; }

    /// <summary>
    /// When set, the listing of .editorconfig waits for it on the thread that asks.
    /// </summary>
    public ManualResetEventSlim? FileListingGate { get; set; }

    /// <summary>
    /// Set each time a listing of .editorconfig has begun.
    /// </summary>
    public SemaphoreSlim FileListingStarted { get; } = new SemaphoreSlim(0);

    public int FileListingCount
    {
        get
        {
            lock(_syncRoot)
            {
                return _fileListings;
            }
        }
    }

    public IReadOnlyList<FileRuleListing> ListFileRules(string settingsPath)
    {
        lock(_syncRoot)
        {
            _fileListings++;
        }

        FileListingStarted.Release();
        FileListingGate?.Wait();
        if(FileListingRefusal != null)
            throw new RuleChangeException(FileListingRefusal);

        return FileListings;
    }

    public IReadOnlyList<RuleDefinition> ListRules(string settingsPath)
    {
        return _inner.ListRules(settingsPath);
    }

    public RuleSettings ReadSettings(string settingsPath)
    {
        return _inner.ReadSettings(settingsPath);
    }

    public string? ReadRequires(string settingsPath)
    {
        return _inner.ReadRequires(settingsPath);
    }

    public RuleSettings ParseSettings(string text)
    {
        return _inner.ParseSettings(text);
    }

    public RuleDefinition? ShowRule(string settingsPath, string ruleId)
    {
        return _inner.ShowRule(settingsPath, ruleId);
    }

    public FileRule? ShowFileRule(string settingsPath, string ruleId)
    {
        return FileListings.SelectMany(listing => listing.Rules).FirstOrDefault(rule => string.Equals(rule.Id, ruleId, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<LanguageCatalog> ListCatalogs(string? languageId, string? artifactTypeName)
    {
        return _inner.ListCatalogs(languageId, artifactTypeName);
    }

    public void SetEnabled(string settingsPath, string ruleId, bool enabled)
    {
        _inner.SetEnabled(settingsPath, ruleId, enabled);
    }

    public void SetSeverity(string settingsPath, string ruleId, Severity severity)
    {
        _inner.SetSeverity(settingsPath, ruleId, severity);
    }

    public void AddRule(string settingsPath, RuleDraft draft)
    {
        _inner.AddRule(settingsPath, draft);
    }

    public void AddRuleBefore(string settingsPath, RuleDraft draft, string beforeRuleId)
    {
        _inner.AddRuleBefore(settingsPath, draft, beforeRuleId);
    }

    public void AddRuleAfter(string settingsPath, RuleDraft draft, string afterRuleId)
    {
        _inner.AddRuleAfter(settingsPath, draft, afterRuleId);
    }

    public void CopyRule(string settingsPath, string ruleId, string newRuleId)
    {
        _inner.CopyRule(settingsPath, ruleId, newRuleId);
    }

    public void ChangeRule(string settingsPath, string ruleId, RuleChange change)
    {
        _inner.ChangeRule(settingsPath, ruleId, change);
    }

    public void RemoveRule(string settingsPath, string ruleId)
    {
        _inner.RemoveRule(settingsPath, ruleId);
    }

    public RuleTryResult TryChangedRule(string settingsPath, string ruleId, RuleChange change, string sourceText, string? fileName)
    {
        return _inner.TryChangedRule(settingsPath, ruleId, change, sourceText, fileName);
    }

    public RuleTryResult TryNewRule(string settingsPath, RuleDraft draft, string sourceText, string? fileName)
    {
        return _inner.TryNewRule(settingsPath, draft, sourceText, fileName);
    }

    public RuleTryResult TryRule(string settingsPath, string ruleId, string sourceText, string? fileName)
    {
        return _inner.TryRule(settingsPath, ruleId, sourceText, fileName);
    }
}