using CF.Adapters.Machine;
using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Languages.CSharp;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Rules.Support;

/// <summary>
/// A folder with a settings file and the sources of a small repository, and the command line to change and ask about it.
/// </summary>
internal sealed class RulesFixture : IDisposable
{
    public const string BracedIfRule = "{\"id\":\"TEAM010\",\"language\":\"csharp\",\"for\":\"if-statement\",\"severity\":\"block\",\"title\":\"No braces around a single-line if\",\"where\":{\"statementsInBody\":1,\"bodyFitsOnOneLine\":true},\"require\":{\"braces\":false}}";

    public const string MethodRule = "{\"id\":\"TEAM011\",\"language\":\"csharp\",\"for\":\"method\",\"severity\":\"block\",\"title\":\"Only public and private methods inside an internal class\",\"where\":{\"containingType\":\"class\",\"containingTypeDeclaredAccess\":\"internal\",\"isOverride\":false},\"require\":{\"declaredAccess\":[\"public\",\"private\"]}}";

    public const string ClockRule = "{\"id\":\"TEAM012\",\"language\":\"csharp\",\"for\":\"usage\",\"severity\":\"block\",\"title\":\"Use IClock, not DateTime.Now\",\"where\":{\"target\":\"System.DateTime.Now\"},\"forbid\":true}";

    private readonly TestFolder _folder = new TestFolder();

    public RulesFixture(string settingsText, IProcessRunner? processRunner = null, ISettingsLock? settingsLock = null, IFileSystem? fileSystem = null)
    {
        Write("commitfence.json", settingsText);
        Commands = new CommandHarness(_folder.Path, processRunner, settingsLock, fileSystem);
    }

    public string Folder
    {
        get { return _folder.Path; }
    }

    public string SettingsPath
    {
        get { return Path.Combine(_folder.Path, "commitfence.json"); }
    }

    public CommandHarness Commands { get; }

    /// <summary>
    /// A fixture whose settings file is in the layout the product writes: the text is written by the product itself.
    /// </summary>
    public static RulesFixture Canonical(params string[] rules)
    {
        var fixture = new RulesFixture("{ \"rules\": [ " + string.Join(", ", rules) + " ] }");
        var id = fixture.Settings().Rules[0].Id;
        Assert.Equal(ExitCode.Passed, fixture.Commands.Run("rules", "enable", id).ExitCode);

        return fixture;
    }

    public string SettingsText()
    {
        return File.ReadAllText(SettingsPath);
    }

    public byte[] SettingsBytes()
    {
        return File.ReadAllBytes(SettingsPath);
    }

    /// <summary>
    /// Reads the text of a settings file as the product does.
    /// </summary>
    public static RuleSettings Parse(string text)
    {
        var languages = new List<ILanguage> { new CSharpLanguage(new FakeDotnet(TestPaths.Temp), new SystemFileSystem(), new TestDataFolder(TestPaths.Temp)) };
        return new SettingsReader(languages).Read(text);
    }

    public RuleSettings Settings()
    {
        return Parse(SettingsText());
    }

    public IReadOnlyList<string> RuleIds()
    {
        return Settings().Rules.Select(rule => rule.Id).ToList();
    }

    public void Write(string relativePath, string text)
    {
        TreeFiles.Write(_folder.Path, relativePath, text);
    }

    /// <summary>
    /// The names of the files in the folder of the settings file, without the folder of the data.
    /// </summary>
    public IReadOnlyList<string> FilesBesideSettings()
    {
        return Directory.EnumerateFiles(_folder.Path).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToList();
    }

    public void Dispose()
    {
        _folder.Dispose();
    }
}
