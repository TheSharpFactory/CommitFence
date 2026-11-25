using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Rules.Settings;

namespace CF.Core.Services;

/// <summary>
/// Reads and writes the settings file through the file system port: answers questions about its rules, changes them,
/// and tries a rule on code.
/// </summary>
public sealed class RuleService : IRuleService
{
    private const string _pastedCodeName = "(pasted code)";

    private readonly ISettingsLock _settingsLock;
    private readonly IReadOnlyList<ILanguage> _languages;
    private readonly SettingsFile _settingsFile;
    private readonly SettingsReader _settingsReader;
    private readonly SettingsSaver _settingsSaver;
    private readonly RuleEditor _ruleEditor;
    private readonly FileRules _fileRules;

    /// <summary>
    /// Creates the service for these languages.
    /// </summary>
    public RuleService(IFileSystem fileSystem, ISettingsLock settingsLock, IReadOnlyList<ILanguage> languages)
    {
        _settingsLock = settingsLock;
        _languages = languages;
        _settingsReader = new SettingsReader(languages);
        _settingsFile = new SettingsFile(fileSystem, _settingsReader);
        _settingsSaver = new SettingsSaver(fileSystem, _settingsReader);
        _ruleEditor = new RuleEditor(languages);
        _fileRules = new FileRules(languages);
    }

    /// <inheritdoc />
    public IReadOnlyList<RuleDefinition> ListRules(string settingsPath)
    {
        return LoadSettings(settingsPath).Rules;
    }

    /// <inheritdoc />
    public RuleDefinition? ShowRule(string settingsPath, string ruleId)
    {
        return LoadSettings(settingsPath).FindRule(ruleId);
    }

    /// <inheritdoc />
    public IReadOnlyList<FileRuleListing> ListFileRules(string settingsPath)
    {
        return _fileRules.List(settingsPath, _settingsFile.ReadIfPresent(settingsPath)?.Strictness);
    }

    /// <inheritdoc />
    public FileRule? ShowFileRule(string settingsPath, string ruleId)
    {
        return _fileRules.Find(settingsPath, ruleId, _settingsFile.ReadIfPresent(settingsPath)?.Strictness);
    }

    /// <inheritdoc />
    public RuleSettings ReadSettings(string settingsPath)
    {
        return LoadSettings(settingsPath);
    }

    /// <inheritdoc />
    public string? ReadRequires(string settingsPath)
    {
        return _settingsFile.ReadRequires(settingsPath);
    }

    /// <inheritdoc />
    public RuleSettings ParseSettings(string text)
    {
        return _settingsFile.Parse(text);
    }

    /// <inheritdoc />
    public IReadOnlyList<LanguageCatalog> ListCatalogs(string? languageId, string? artifactTypeName)
    {
        var languages = _languages.Where(language => languageId == null || language.Id == languageId).ToList();
        if(languages.Count == 0)
            throw new RuleChangeException($"'{languageId}' is not a language the product has. Known languages: {string.Join(", ", _languages.Select(language => language.Id))}.");

        return languages.Select(language => CreateCatalog(language, artifactTypeName)).ToList();
    }

    /// <inheritdoc />
    public void SetEnabled(string settingsPath, string ruleId, bool enabled)
    {
        Change(settingsPath, ruleId, rule => rule.WithEnabled(enabled));
    }

    /// <inheritdoc />
    public void SetSeverity(string settingsPath, string ruleId, Severity severity)
    {
        Change(settingsPath, ruleId, rule => rule.WithSeverity(severity));
    }

    /// <inheritdoc />
    public void AddRule(string settingsPath, RuleDraft draft)
    {
        WhileHoldingLock(settingsPath, () => AddRuleLocked(settingsPath, draft));
    }

    private void AddRuleLocked(string settingsPath, RuleDraft draft)
    {
        var text = ReadSettingsText(settingsPath);
        var settings = ParseSettings(text);
        var rules = settings.Rules.Append(_ruleEditor.Create(draft)).ToList();

        Save(settingsPath, text, settings, rules);
    }

    /// <inheritdoc />
    public void AddRuleBefore(string settingsPath, RuleDraft draft, string beforeRuleId)
    {
        WhileHoldingLock(settingsPath, () => AddNextTo(settingsPath, beforeRuleId, false, _ => _ruleEditor.Create(draft)));
    }

    /// <inheritdoc />
    public void AddRuleAfter(string settingsPath, RuleDraft draft, string afterRuleId)
    {
        WhileHoldingLock(settingsPath, () => AddNextTo(settingsPath, afterRuleId, true, _ => _ruleEditor.Create(draft)));
    }

    /// <inheritdoc />
    public void CopyRule(string settingsPath, string ruleId, string newRuleId)
    {
        RuleEditor.CheckNewId(newRuleId);
        WhileHoldingLock(settingsPath, () => AddNextTo(settingsPath, ruleId, true, source => CopyOf(source, newRuleId)));
    }

    private static RuleDefinition CopyOf(RuleDefinition source, string newRuleId)
    {
        return new RuleDefinition(newRuleId, source.Language, source.ArtifactType, source.Severity, $"Copy of {source.Title}", source.Where, source.Require, source.Forbid, source.Paths, source.Message, source.Why, source.Enabled);
    }

    private void AddNextTo(string settingsPath, string anchorId, bool after, Func<RuleDefinition, RuleDefinition> build)
    {
        var text = ReadSettingsText(settingsPath);
        var settings = ParseSettings(text);
        var anchor = FindExistingRule(settingsPath, settings, anchorId);
        var rules = settings.Rules.ToList();
        rules.Insert(rules.FindIndex(rule => rule.Id == anchorId) + (after ? 1 : 0), build(anchor));

        Save(settingsPath, text, settings, rules);
    }

    /// <inheritdoc />
    public void ChangeRule(string settingsPath, string ruleId, RuleChange change)
    {
        Change(settingsPath, ruleId, rule => _ruleEditor.Change(rule, change));
    }

    /// <inheritdoc />
    public void RemoveRule(string settingsPath, string ruleId)
    {
        WhileHoldingLock(settingsPath, () => RemoveRuleLocked(settingsPath, ruleId));
    }

    private void RemoveRuleLocked(string settingsPath, string ruleId)
    {
        var text = ReadSettingsText(settingsPath);
        var settings = ParseSettings(text);
        FindExistingRule(settingsPath, settings, ruleId);
        var rules = settings.Rules.Where(rule => rule.Id != ruleId).ToList();

        Save(settingsPath, text, settings, rules);
    }

    /// <inheritdoc />
    public RuleTryResult TryChangedRule(string settingsPath, string ruleId, RuleChange change, string sourceText, string? fileName)
    {
        var text = ReadSettingsText(settingsPath);
        var settings = ParseSettings(text);
        FindExistingRule(settingsPath, settings, ruleId);
        var rules = settings.Rules.Select(rule => rule.Id == ruleId ? _ruleEditor.Change(rule, change) : rule).ToList();

        return TryAfterChecking(text, settings, rules, rules.First(rule => rule.Id == ruleId), sourceText, fileName);
    }

    /// <inheritdoc />
    public RuleTryResult TryNewRule(string settingsPath, RuleDraft draft, string sourceText, string? fileName)
    {
        var text = ReadSettingsText(settingsPath);
        var settings = ParseSettings(text);
        var created = _ruleEditor.Create(draft);
        var rules = settings.Rules.Append(created).ToList();

        return TryAfterChecking(text, settings, rules, created, sourceText, fileName);
    }

    /// <inheritdoc />
    public RuleTryResult TryRule(string settingsPath, string ruleId, string sourceText, string? fileName)
    {
        var rule = FindExistingRule(settingsPath, LoadSettings(settingsPath), ruleId);
        return Try(rule, sourceText, fileName);
    }

    private RuleTryResult TryAfterChecking(string currentText, RuleSettings settings, IReadOnlyList<RuleDefinition> rules, RuleDefinition rule, string sourceText, string? fileName)
    {
        _settingsSaver.CheckWouldSave(new RuleSettings(settings.Requires, rules, settings.Structure, settings.ConfiguredStrictness), NewlineOf(currentText));

        return Try(rule, sourceText, fileName);
    }

    private RuleTryResult Try(RuleDefinition rule, string sourceText, string? fileName)
    {
        var language = _languages.First(candidate => candidate.Id == rule.Language);
        if(MeaningNeeds.Has(rule, language))
            return new RuleTryResult(TryStatus.NeedsBuild, new List<ArtifactVerdict>(), "the rule needs what the compiler knows about the code, so it cannot be tried without a build", null, null);

        try
        {
            var artifacts = language.Reader.ReadPiece(new CodeFile(fileName ?? _pastedCodeName, language.Id, sourceText));
            return new RuleTryResult(TryStatus.Tried, RuleTrial.Judge(rule, artifacts, fileName), string.Empty, null, null);
        }
        catch(CodeReadException exception)
        {
            return new RuleTryResult(TryStatus.DoesNotParse, new List<ArtifactVerdict>(), exception.Message, exception.Line, exception.Column);
        }
    }

    private static LanguageCatalog CreateCatalog(ILanguage language, string? artifactTypeName)
    {
        var types = language.Catalog.ArtifactTypes.Where(type => artifactTypeName == null || type.Name == artifactTypeName).ToList();
        if(types.Count == 0)
            throw new RuleChangeException($"'{artifactTypeName}' is not an artifact type of {language.Id}. Known types: {string.Join(", ", language.Catalog.ArtifactTypes.Select(type => type.Name))}.");

        return new LanguageCatalog(language.Id, language.Name, types);
    }

    private void WhileHoldingLock(string settingsPath, Action change)
    {
        using var held = SettingsSaver.TakeLock(_settingsLock, settingsPath);
        change();
    }

    private void Change(string settingsPath, string ruleId, Func<RuleDefinition, RuleDefinition> change)
    {
        WhileHoldingLock(settingsPath, () => ChangeLocked(settingsPath, ruleId, change));
    }

    private void ChangeLocked(string settingsPath, string ruleId, Func<RuleDefinition, RuleDefinition> change)
    {
        var text = ReadSettingsText(settingsPath);
        var settings = ParseSettings(text);
        FindExistingRule(settingsPath, settings, ruleId);
        var rules = settings.Rules.Select(rule => rule.Id == ruleId ? change(rule) : rule).ToList();

        Save(settingsPath, text, settings, rules);
    }

    private RuleDefinition FindExistingRule(string settingsPath, RuleSettings settings, string ruleId)
    {
        return settings.FindRule(ruleId) ?? throw (_fileRules.RefusalFor(settingsPath, ruleId) ?? new RuleChangeException($"The settings file has no rule '{ruleId}'."));
    }

    private void Save(string settingsPath, string currentText, RuleSettings settings, IReadOnlyList<RuleDefinition> rules)
    {
        _settingsSaver.Save(settingsPath, currentText, new RuleSettings(settings.Requires, rules, settings.Structure, settings.ConfiguredStrictness));
    }

    private static string NewlineOf(string text)
    {
        return text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    }

    private string ReadSettingsText(string settingsPath)
    {
        return _settingsFile.ReadText(settingsPath);
    }

    private RuleSettings LoadSettings(string settingsPath)
    {
        return _settingsFile.Read(settingsPath);
    }
}