using CF.App.Rules;
using CF.App.Rules.Page;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;

namespace CF.App.Tests.Support;

/// <summary>
/// The Rules screen over a repository folder in a temporary place: the real rule service reading and writing its settings file,
/// the rules of .editorconfig as the test sets them, and settings changes that happen when the test raises them.
/// </summary>
internal sealed class RulesHost : IDisposable
{
    private readonly TestFolder _folder = new TestFolder();

    public RulesHost(string? settings = SettingsText.Four, IReadOnlyList<FileRule>? fileRules = null, Action<RuleServiceDouble>? prepare = null)
    {
        RepositoryPath = Path.Combine(_folder.Path, "invoicing");
        Directory.CreateDirectory(RepositoryPath);
        SettingsPath = Path.Combine(RepositoryPath, "commitfence.json");
        if(settings != null)
            File.WriteAllText(SettingsPath, settings);

        Service = new RuleServiceDouble(Path.Combine(_folder.Path, "data"));
        SetFileRules(fileRules ?? new List<FileRule>());
        prepare?.Invoke(Service);
        var catalog = new RuleCatalog(Service.ListCatalogs(null, null));
        Model = new RulesViewModel(Service, Samples.Folder(RepositoryPath), catalog, Settings, EditorConfig);
        Loaded = Signals.FileRulesArrived(Model);
    }

    public string RepositoryPath { get; }

    public string SettingsPath { get; }

    public RuleServiceDouble Service { get; }

    public ManualChanges Settings { get; } = new ManualChanges();

    public ManualChanges EditorConfig { get; } = new ManualChanges();

    public RulesViewModel Model { get; }

    /// <summary>
    /// Completes when the rules of .editorconfig have first been listed.
    /// </summary>
    public Task Loaded { get; }

    /// <summary>
    /// Rules of .editorconfig as the listing of the rule service answers them, for the next time they are asked for.
    /// </summary>
    public void SetFileRules(IReadOnlyList<FileRule> rules)
    {
        Service.FileListings = rules.Count == 0 ? new List<FileRuleListing>() : new List<FileRuleListing> { new FileRuleListing("csharp", ".editorconfig", rules, new List<string>()) };
    }

    public byte[] SettingsBytes()
    {
        return File.ReadAllBytes(SettingsPath);
    }

    public IReadOnlyList<RuleDefinition> StoredRules()
    {
        return Service.ListRules(SettingsPath);
    }

    /// <summary>
    /// The ids of the rows the list draws, team rules and rules of .editorconfig, in the order of the list.
    /// </summary>
    public IReadOnlyList<string> RowIds()
    {
        return Model.Items.Select(item => item switch
        {
            TeamRuleRowViewModel team => team.Id,
            FileRuleRowViewModel file => file.Id,
            _ => string.Empty
        }).Where(id => id.Length > 0).ToList();
    }

    public TeamRuleRowViewModel Row(string id)
    {
        return Model.Items.OfType<TeamRuleRowViewModel>().First(row => row.Id == id);
    }

    /// <summary>
    /// Opens the page of a team rule, as choosing its row does.
    /// </summary>
    public RulePageViewModel Open(string id)
    {
        Model.SelectedItem = Row(id);
        return Model.CurrentPage ?? throw new InvalidOperationException("no page is open for " + id);
    }

    public static FileRule ScopedRule()
    {
        const string id = "IDE0055";
        var main = new FileRulePlace("*.cs", "error", FileRuleState.On, Severity.Block, 14,
            [new FileLine(1, "[*.cs]"), new FileLine(14, $"dotnet_diagnostic.{id}.severity = error")], _ => true);
        var tests = new FileRulePlace("tests/**/*.cs", "suggestion", FileRuleState.On, Severity.Warn, 18,
            [new FileLine(16, "[tests/**/*.cs]"), new FileLine(17, "# Formatting in tests"), new FileLine(18, $"dotnet_diagnostic.{id}.severity = suggestion")],
            path => path.StartsWith("tests/", StringComparison.Ordinal));
        var generated = new FileRulePlace("**/Generated/*.cs", "none", FileRuleState.Off, null, 21,
            [new FileLine(20, "[**/Generated/*.cs]"), new FileLine(21, $"dotnet_diagnostic.{id}.severity = none")],
            path => path.Contains("/Generated/", StringComparison.Ordinal));
        var designer = new FileRulePlace("*.Designer.cs", "none", FileRuleState.Off, null, 24,
            [new FileLine(23, "[*.Designer.cs]"), new FileLine(24, $"dotnet_diagnostic.{id}.severity = none")],
            path => path.EndsWith(".Designer.cs", StringComparison.Ordinal));
        return new FileRule(id, "Fix formatting", ".editorconfig", "C#", main, [tests, generated, designer]);
    }

    public static FileRule FileRule(string id, string? title, FileRuleState state, Severity? severity, int line, string setting = "error")
    {
        var lines = new List<FileLine> { new FileLine(line - 1, "[*.cs]"), new FileLine(line, $"dotnet_diagnostic.{id}.severity = {setting}"), new FileLine(line + 1, "# the next rule") };
        return new FileRule(id, title, setting, state, severity, ".editorconfig", line, lines);
    }

    public void Dispose()
    {
        Model.Dispose();
        _folder.Dispose();
    }
}