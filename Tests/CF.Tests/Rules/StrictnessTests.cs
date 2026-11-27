using System.Text;
using CF.Adapters.Machine;
using CF.Adapters.Storage;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CF.Tests.Review.Support;
using CF.Tests.Rules.Support;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Rules;

/// <summary>Strictness settings, the shared mapping, and settings changes through the command line.</summary>
public sealed class StrictnessTests
{
    private const string _custom = """{"error":"off","warning":"block","suggestion":"warn"}""";
    private const string _structure = """
        "structure": { "modules": { "app": ["folder:App"], "core": ["folder:Core"] },
                       "directions": { "allow": ["app -> core"] } }
        """;

    [Theory]
    [InlineData(StrictnessPreset.Quiet, Severity.Block, Severity.Warn, null)]
    [InlineData(StrictnessPreset.Standard, Severity.Block, Severity.Warn, Severity.Warn)]
    [InlineData(StrictnessPreset.Strict, Severity.Block, Severity.Block, Severity.Warn)]
    [InlineData(StrictnessPreset.Custom, null, Severity.Block, Severity.Warn)]
    public void SeverityWordsHaveThePresetOrCustomEffect(StrictnessPreset preset, Severity? error, Severity? warning, Severity? suggestion)
    {
        var mapping = preset == StrictnessPreset.Custom ? RulesFixture.Parse(Settings(_custom)).Strictness : Strictness.For(preset);

        Assert.Equal(error, mapping.EffectOf("error"));
        Assert.Equal(warning, mapping.EffectOf("warning"));
        Assert.Equal(suggestion, mapping.EffectOf("suggestion"));
        Assert.Null(mapping.EffectOf("silent"));
        Assert.Null(mapping.EffectOf("none"));
    }

    [Theory]
    [InlineData("{}", StrictnessPreset.Standard, null, 0, 0)]
    [InlineData("{\"strictness\":\"quiet\"}", StrictnessPreset.Quiet, null, 0, 0)]
    [InlineData("{\"strictness\":\"standard\"}", StrictnessPreset.Standard, null, 0, 0)]
    [InlineData("{\"strictness\":\"strict\"}", StrictnessPreset.Strict, null, 0, 0)]
    [InlineData("{\"strictness\":" + _custom + "}", StrictnessPreset.Custom, null, 0, 0)]
    [InlineData("{\n  \"strictness\": \"loud\"\n}", null, "Strictness must be", 2, 3)]
    [InlineData("{\n  \"strictness\": {\"error\":\"block\"}\n}", null, "needs a value for 'warning'", 2, 3)]
    [InlineData("{\n  \"strictness\": {\n    \"note\":\"warn\"\n  }\n}", null, "key 'note' is not known", 3, 5)]
    [InlineData("{\n  \"strictness\": {\n    \"error\":true\n  }\n}", null, "effect of 'error' must be", 3, 5)]
    public void StrictnessIsReadStrictly(string text, StrictnessPreset? preset, string? message, int line, int column)
    {
        if(preset is { } choice)
        {
            Assert.Equal(choice, RulesFixture.Parse(text).Strictness.Preset);
            return;
        }

        var error = Assert.Throws<SettingsException>(() => RulesFixture.Parse(text));
        Assert.Contains(message!, error.Message, StringComparison.Ordinal);
        Assert.Equal(line, error.Line);
        Assert.Equal(column, error.Column);
    }

    [Fact]
    public void EachEditorKeepsTheSettingsItDidNotChange()
    {
        var text = "{ \"requires\": \"1.0\", \"strictness\": " + _custom + ", \"rules\": [" + RulesFixture.BracedIfRule + "], " + _structure + " }";
        using var fixture = new RulesFixture(text);
        var before = fixture.Settings();

        Assert.Equal(ExitCode.Passed, fixture.Commands.Run("rules", "set", "TEAM010", "--title", "Changed title").ExitCode);
        var afterRule = fixture.Settings();
        Assert.Equal(before.ConfiguredStrictness, afterRule.ConfiguredStrictness);
        Assert.Equal(before.Structure.Describe(), afterRule.Structure.Describe());

        Assert.Equal(ExitCode.Passed, fixture.Commands.Run("settings", "set", "strictness", "strict").ExitCode);
        var afterSetting = fixture.Settings();
        Assert.Equal(StrictnessPreset.Strict, afterSetting.Strictness.Preset);
        Assert.Equal("Changed title", Assert.Single(afterSetting.Rules).Title);
        Assert.Equal(afterRule.Structure.Describe(), afterSetting.Structure.Describe());
        Assert.Equal(before.Requires, afterSetting.Requires);
    }

    [Fact]
    public void OwnMappingReadsBackAndAChangedFileRefusesItsUndo()
    {
        using var fixture = new RulesFixture("""{"$schema":"https://example.org/schema.json","strictness":"quiet","rules":[]}""");
        var service = new SettingsService(new SystemFileSystem(), new FileSettingsLock(new TestDataFolder(fixture.Folder)), []);

        service.ChangeStrictness(fixture.Folder, current => current with { Preset = StrictnessPreset.Custom });
        Assert.Equal(new Strictness(StrictnessPreset.Custom, Severity.Block, Severity.Warn, null), fixture.Settings().Strictness);
        var edited = service.ChangeStrictness(fixture.Folder, current => current with { Error = null }).After;
        Assert.Equal(edited, fixture.Settings().Strictness);
        var saved = fixture.SettingsText();
        Assert.StartsWith("""{"$schema":"https://example.org/schema.json",""", saved, StringComparison.Ordinal);
        Assert.Contains("\"strictness\":{ \"error\": \"off\"", saved, StringComparison.Ordinal);
        Assert.EndsWith(""","rules":[]}""", saved, StringComparison.Ordinal);

        var replacement = service.ChangeStrictness(fixture.Folder, _ => Strictness.Standard);
        service.ChangeStrictness(fixture.Folder, _ => replacement.Before, replacement.Revision);
        Assert.Equal(edited, fixture.Settings().Strictness);

        var second = service.ChangeStrictness(fixture.Folder, _ => Strictness.Standard);
        var newer = fixture.SettingsText() + " ";
        fixture.Write(SettingsFileName.Default, newer);
        var refused = Assert.Throws<RuleChangeException>(() => service.ChangeStrictness(fixture.Folder, _ => second.Before, second.Revision));
        Assert.Equal("Could not undo: commitfence.json changed on disk.", refused.Message);
        Assert.Equal(newer, fixture.SettingsText());
    }

    [Fact]
    public void UndoRefusesAByteOrderMarkAddedAfterSaving()
    {
        using var fixture = new RulesFixture(Settings("\"quiet\""));
        var service = new SettingsService(new SystemFileSystem(), new FileSettingsLock(new TestDataFolder(fixture.Folder)), []);
        var saved = service.ChangeStrictness(fixture.Folder, _ => Strictness.Standard);
        File.WriteAllText(fixture.SettingsPath, fixture.SettingsText(), Encoding.UTF8);
        var withMark = fixture.SettingsBytes();

        var refused = Assert.Throws<RuleChangeException>(() => service.ChangeStrictness(fixture.Folder, _ => saved.Before, saved.Revision));
        Assert.Equal("Could not undo: commitfence.json changed on disk.", refused.Message);
        Assert.Equal(withMark, fixture.SettingsBytes());
    }

    [Fact]
    public void StrictnessSaveRefusesAByteOrderMarkAddedWhileWorking()
    {
        byte[]? withMark = null;
        var files = new ObservedFileSystem(_ => { }, (path, _) =>
        {
            if(!path.EndsWith(".new", StringComparison.Ordinal))
                return;

            var settingsPath = Path.Combine(Path.GetDirectoryName(path)!, SettingsFileName.Default);
            File.WriteAllText(settingsPath, File.ReadAllText(settingsPath), Encoding.UTF8);
            withMark = File.ReadAllBytes(settingsPath);
        });
        using var fixture = new RulesFixture(Settings("\"quiet\""), fileSystem: files);
        var service = new SettingsService(files, new FileSettingsLock(new TestDataFolder(fixture.Folder)), []);

        var refused = Assert.Throws<RuleChangeException>(() => service.ChangeStrictness(fixture.Folder, _ => Strictness.Standard));
        Assert.Contains("changed while this command was working", refused.Message, StringComparison.Ordinal);
        Assert.NotNull(withMark);
        Assert.Equal(withMark, fixture.SettingsBytes());
    }

    [Fact]
    public void SettingsCommandsShowThePolicyAndRefuseStaleOrInvalidWrites()
    {
        var changed = false;
        var newer = Settings("\"quiet\"");
        var files = new ObservedFileSystem(_ => { }, (path, _) =>
        {
            if(!changed && path.EndsWith(".new", StringComparison.Ordinal))
            {
                changed = true;
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, SettingsFileName.Default), newer);
            }
        });
        using var sdk = new FakeSdk();
        using var fixture = new RulesFixture(Settings(_custom), sdk.Runner(true), fileSystem: files);
        fixture.Write("Program.cs", "class Program { }");
        fixture.Write(".editorconfig", "[*.cs]\ndotnet_diagnostic.CA1000.severity = warning\ndotnet_diagnostic.CA1001.severity = suggestion\ndotnet_diagnostic.CA1002.severity = error\n");

        var show = fixture.Commands.Run("settings", "show");
        Assert.Equal(ExitCode.Passed, show.ExitCode);
        Assert.Contains("Strictness: Custom", show.Output, StringComparison.Ordinal);
        Assert.Contains("Custom (in force)", show.Output, StringComparison.Ordinal);
        Assert.Contains("With Custom, 1 rules of the file block a push, 1 only warn and 1 are off.", show.Output, StringComparison.Ordinal);

        var stale = fixture.Commands.Run("settings", "set", "strictness", "strict");
        Assert.Equal(ExitCode.BadSettingsOrUsage, stale.ExitCode);
        Assert.Contains("changed while this command was working", stale.Error, StringComparison.Ordinal);
        Assert.Equal(newer, fixture.SettingsText());

        var set = fixture.Commands.Run("settings", "set", "strictness", "strict");
        Assert.Equal(ExitCode.Passed, set.ExitCode);
        Assert.Equal("Strictness set to Strict." + Environment.NewLine, set.Output);
        Assert.Equal(StrictnessPreset.Strict, fixture.Settings().Strictness.Preset);

        var invalid = Settings("\"loud\"");
        fixture.Write(SettingsFileName.Default, invalid);
        var refused = fixture.Commands.Run("settings", "set", "strictness", "strict");
        Assert.Equal(ExitCode.BadSettingsOrUsage, refused.ExitCode);
        Assert.Contains("Strictness must be", refused.Error, StringComparison.Ordinal);
        Assert.Equal(invalid, fixture.SettingsText());
    }

    private static string Settings(string strictness)
    {
        return "{ \"strictness\": " + strictness + ", \"rules\": [] }";
    }
}
