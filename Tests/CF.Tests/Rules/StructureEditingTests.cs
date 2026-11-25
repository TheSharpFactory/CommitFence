using System.Text.Json;
using CF.Adapters.Machine;
using CF.Adapters.Storage;
using CF.Core.Code;
using CF.Core.Review;
using CF.Core.Services;
using CF.Tests.Review.Support;
using CF.Tests.Rules.Support;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Rules;

/// <summary>How structure edits and the command line read, save and undo the settings.</summary>
public sealed class StructureEditingTests
{
    private const string _settings = """
        {
          "strictness": {"error":"block","warning":"warn","suggestion":"off"},
          "rules": [
            {
              "id":"TEAM010","language":"csharp","for":"if-statement","severity":"block","enabled":true,
              "title":"Braces","where":{"statementsInBody":1},"require":{"braces":false}
            }
          ],
          "structure": {
            "modules": {"web":["folder:src/Web/**","project:Web"],"core":["namespace:Shop.Core"],"store":["project:Store"]},
            "directions": {"allow":["web -> core","core -> store"],"noCycles":true},
            "references":[{"from":"project:Core","mustNotReference":["project:Web","package:Ui*"]}],
            "layout":[{"folder":"src/Web","filesNamed":"*Page.cs"},{"rule":"one-type-per-file","under":"src"}]
          }
        }
        """;

    [Theory]
    [InlineData("add-module", "docs", null, "web,core,store,docs", "", "web -> core,core -> store")]
    [InlineData("rename-module", "web", "http", "http,core,store", "folder:src/Web/**,project:Web", "http -> core,core -> store")]
    [InlineData("remove-module", "web", null, "core,store", "", "core -> store")]
    [InlineData("add-part", "web", "namespace:Shop.Web", "web,core,store", "folder:src/Web/**,project:Web,namespace:Shop.Web", "web -> core,core -> store")]
    [InlineData("remove-part", "web", "folder:src/Web/**", "web,core,store", "project:Web", "web -> core,core -> store")]
    [InlineData("allow", "web", "store", "web,core,store", "folder:src/Web/**,project:Web", "web -> core,core -> store,web -> store")]
    [InlineData("stop-allowing", "web", "core", "web,core,store", "folder:src/Web/**,project:Web", "core -> store")]
    public void ChangesReadBackAndKeepUnrelatedSettings(string action, string name, string? value, string names, string parts, string directions)
    {
        using var fixture = new RulesFixture(_settings);
        var service = Service(fixture);
        var change = Apply(service, fixture.Folder, action, name, value);

        var settings = fixture.Settings();
        Assert.Equal(names, string.Join(",", settings.Structure.Modules.Select(module => module.Name)));
        var edited = settings.Structure.Modules.SingleOrDefault(module => module.Name == (action == "rename-module" ? value : name));
        Assert.Equal(parts, string.Join(",", edited?.Selectors ?? []));
        Assert.Equal(directions, string.Join(",", settings.Structure.Directions.Allow));
        Assert.True(settings.Structure.Directions.NoCycles);
        AssertUnrelatedSettings(fixture.SettingsText());

        service.Undo(fixture.Folder, change);
        Assert.Equal(_settings, fixture.SettingsText());
    }

    [Theory]
    [InlineData("allow", true)]
    [InlineData("forbid", false)]
    public void ModuleChangesReachEveryDirectionAndUndoRestoresOrder(string list, bool allowed)
    {
        using var fixture = new RulesFixture(_settings.Replace("\"allow\"", $"\"{list}\"", StringComparison.Ordinal));
        var service = Service(fixture);
        service.RenameModule(fixture.Folder, "core", "logic");

        var renamed = fixture.SettingsText();
        var directions = fixture.Settings().Structure.Directions;
        Assert.Equal("web -> logic,logic -> store", string.Join(",", allowed ? directions.Allow : directions.Forbid));
        Assert.Equal(allowed, directions.Permits("web", "logic"));
        Assert.Equal(allowed, directions.Permits("logic", "store"));

        var removed = service.RemoveModule(fixture.Folder, "logic");
        var after = service.Show(fixture.Folder);
        Assert.Equal("web,store", string.Join(",", after.Modules.Select(item => item.Name)));
        Assert.Empty((allowed ? after.Directions.Allow : after.Directions.Forbid)!);
        Assert.Equal(allowed, after.Directions.Allow != null);
        Assert.Equal(!allowed, after.Directions.Forbid != null);
        Assert.Equal(!allowed, after.AllowedDirections.Contains(new StructureDirection("web", "store")));

        service.Undo(fixture.Folder, removed);
        Assert.Equal(renamed, fixture.SettingsText());
    }

    [Theory]
    [InlineData("allow", true, "web", "store", true, 3)]
    [InlineData("allow", true, "web", "core", false, 1)]
    [InlineData("forbid", false, "web", "core", true, 5)]
    [InlineData("forbid", false, "web", "store", false, 3)]
    public void DirectionEditsAndReportsSpeakInPermissions(string list, bool allowList, string from, string to, bool permit, int count)
    {
        using var fixture = new RulesFixture(_settings.Replace("\"allow\"", $"\"{list}\"", StringComparison.Ordinal));
        var service = Service(fixture);
        service.SetDirection(fixture.Folder, from, to, permit);

        var report = service.Show(fixture.Folder);
        Assert.Equal(count, report.AllowedDirections.Count);
        Assert.Equal(permit, fixture.Settings().Structure.Directions.Permits(from, to));
        Assert.Equal(permit, report.AllowedDirections.Contains(new StructureDirection(from, to)));
        Assert.Equal(permit, report.Modules.Single(module => module.Name == from).MayUse.Contains(to));
        Assert.Equal(permit, report.Modules.Single(module => module.Name == to).UsedBy.Contains(from));
        Assert.Equal(allowList, report.Directions.Allow != null);
        AssertUnrelatedSettings(fixture.SettingsText());
    }

    [Theory]
    [InlineData("add-module", "web", null, "already exists")]
    [InlineData("allow", "web", "missing", "'missing' is not a module")]
    [InlineData("add-part", "web", "package:Ui", "is not a kind of selector")]
    [InlineData("remove-part", "web", "project:Missing", "does not have the part")]
    [InlineData("deny-write", "docs", null, "could not be written")]
    [InlineData("stale-write", "docs", null, "changed while this command was working")]
    public void RefusalsLeaveTheFileAlone(string action, string name, string? value, string reason)
    {
        const string newer = "{\"rules\":[]}";
        var files = new ObservedFileSystem(_ => { }, (path, _) =>
        {
            if(path.EndsWith(".new", StringComparison.Ordinal))
            {
                if(action == "deny-write")
                    throw new UnauthorizedAccessException("The replacement cannot be read.");

                if(action == "stale-write")
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "commitfence.json"), newer);
            }
        });
        using var fixture = new RulesFixture(_settings, fileSystem: files);
        var args = new List<string> { "structure", "set", action.EndsWith("-write", StringComparison.Ordinal) ? "add-module" : action, name };
        if(value != null)
            args.Add(value);

        var result = fixture.Commands.Run(args.ToArray());

        Assert.Equal(ExitCode.BadSettingsOrUsage, result.ExitCode);
        Assert.Contains(reason, result.Error, StringComparison.Ordinal);
        Assert.Equal(action == "stale-write" ? newer : _settings, fixture.SettingsText());
        Assert.Equal("commitfence.json", Assert.Single(fixture.FilesBesideSettings()));
    }

    [Theory]
    [InlineData("\"allow\":[]", "Allowed web to use core.")]
    [InlineData("", "Allowed web to use core. Every other use between modules is now a finding.")]
    public void CommandRunnerShowsTheFileAndWritesTwoDifferentChanges(string directions, string confirmation)
    {
        using var fixture = new RulesFixture("{\"rules\":[],\"structure\":{\"modules\":{\"core\":[]},\"directions\":{" + directions + "}}}");
        var add = fixture.Commands.Run("structure", "set", "add-module", "web");
        var allow = fixture.Commands.Run("structure", "set", "allow", "web", "core");
        var show = fixture.Commands.Run("structure", "show");

        Assert.All(new List<CommandResult> { add, allow, show }, result => Assert.Equal(ExitCode.Passed, result.ExitCode));
        Assert.Equal("Added module 'web'." + Environment.NewLine, add.Output);
        Assert.Equal(confirmation + Environment.NewLine, allow.Output);
        Assert.Equal(new List<string> { "Modules", "  core", "  web", "Allowed directions", "  web -> core", "2 modules, 1 allowed direction",
            "Not judged yet: no review has run" }, show.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("web -> core", Assert.Single(fixture.Settings().Structure.Directions.Allow).ToString());
    }

    [Fact]
    public void OtherSettingsEditsKeepUnjudgedStructureAndReviewsNameIt()
    {
        using var fixture = new RulesFixture(_settings);
        var before = fixture.Settings().Structure.Describe();
        var change = fixture.Commands.Run("settings", "set", "strictness", "quiet");
        Assert.Equal(ExitCode.Passed, change.ExitCode);
        Assert.Equal(before, fixture.Settings().Structure.Describe());
        fixture.Write("Caller.cs", "namespace Demo;\n\nclass Caller { }\n");
        var review = fixture.Commands.Run("review", "--files", "Caller.cs");
        Assert.Equal(ExitCode.Incomplete, review.ExitCode);
        Assert.Contains("not run: structure references", review.Output, StringComparison.Ordinal);
        Assert.Contains("not run: structure layout", review.Output, StringComparison.Ordinal);
        Assert.Contains("not run: structure noCycles", review.Output, StringComparison.Ordinal);
    }

    private static StructureChange Apply(StructureService service, string folder, string action, string name, string? value)
    {
        return action switch
        {
            "add-module" => service.AddModule(folder, name),
            "rename-module" => service.RenameModule(folder, name, value!),
            "remove-module" => service.RemoveModule(folder, name),
            "add-part" => service.AddPart(folder, name, value!),
            "remove-part" => service.RemovePart(folder, name, value!),
            _ => service.SetDirection(folder, name, value!, action == "allow")
        };
    }

    private static StructureService Service(RulesFixture fixture)
    {
        var data = fixture.Commands.DataFolder;
        var files = new SystemFileSystem();
        return new StructureService(files, new FileSettingsLock(data), new List<ILanguage> { new TestLanguage() },
            new FakeGit(fixture.Folder), new FileStructureJudgments(data, files));
    }

    private static void AssertUnrelatedSettings(string after)
    {
        using var oldDocument = JsonDocument.Parse(_settings);
        using var newDocument = JsonDocument.Parse(after);
        var before = oldDocument.RootElement;
        var updated = newDocument.RootElement;
        Assert.True(JsonElement.DeepEquals(before.GetProperty("rules"), updated.GetProperty("rules")));
        Assert.True(JsonElement.DeepEquals(before.GetProperty("strictness"), updated.GetProperty("strictness")));
        foreach(var key in new List<string> { "references", "layout" })
            Assert.True(JsonElement.DeepEquals(before.GetProperty("structure").GetProperty(key), updated.GetProperty("structure").GetProperty(key)));
    }
}
