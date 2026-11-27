using System.Text.Json;
using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CF.Languages.CSharp.Catalog;
using CF.Tests.Rules.Support;
using Xunit;

namespace CF.Tests.Rules;

/// <summary>Checks the JSON Schema of commitfence.json against the catalog and against what the settings reader accepts.</summary>
public sealed class SettingsSchemaTests
{
    private const string _writeVariable = "CF_WRITE_SCHEMA";
    private const string _address = "https://commitfence.com/schema/commitfence.schema.json";
    private const string _allFeatures = """
        {
            "$schema": "https://commitfence.com/schema/commitfence.schema.json",
            "requires": "1.0.0.7",
            "strictness": { "error": "block", "warning": "warn", "suggestion": "off" },
            "rules": [
                {
                    "id": "A1", "language": "csharp", "for": "method", "severity": "warn", "enabled": false, "title": "Parameters and names",
                    "where": { "parameterCount": { "atLeast": 1, "atMost": 5 }, "name": { "matches": "Get*" }, "isOverride": [true, false], "writtenAccess": { "not": ["none", "private"] }, "lines": [3, 4] },
                    "require": { "declaredAccess": { "matches": "pub*" }, "attributes": { "not": "Obsolete" } },
                    "paths": ["src/**"], "message": "Look again.", "why": "Because."
                },
                { "id": "A2", "language": "csharp", "for": "usage", "severity": "block", "title": "No clock", "where": { "target": "System.DateTime.Now" }, "forbid": true },
                { "id": "A3", "language": "csharp", "for": "file", "severity": "block", "title": "Small files", "require": { "typesInFile": { "atMost": 1 }, "name": ["A.cs", "B.cs"] } }
            ],
            "structure": {
                "modules": { "web": ["namespace:*.Web"], "app": ["project:App", "folder:src/app"] },
                "directions": { "allow": ["web -> app"], "forbid": ["app->web"], "noCycles": true },
                "references": [{ "from": "web" }],
                "layout": []
            }
        }
        """;

    [Fact]
    public void TheSchemaFileIsTheOneTheCatalogGives()
    {
        var generated = SettingsSchemaGenerator.Generate("csharp", new CSharpArtifactCatalog());
        var path = Path.Combine(RepositoryFolder(), "schema", "commitfence.schema.json");
        if(Environment.GetEnvironmentVariable(_writeVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, generated);
        }

        var current = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal) : string.Empty;

        Assert.True(current == generated, $"schema/commitfence.schema.json does not match the catalog: run the tests with {_writeVariable}=1 to write the file again.");
    }

    [Fact]
    public void FilesTheReaderAcceptsPassTheSchema()
    {
        var schema = LoadSchema();
        var accepted = new Dictionary<string, string>
        {
            ["this repository"] = File.ReadAllText(Path.Combine(RepositoryFolder(), "commitfence.json")),
            ["the file setup writes"] = SetupService.StarterSettings,
            ["every kind of condition"] = _allFeatures,
            ["the same words spelled with escapes"] = """{ "rules": [ { "id": "A", "language": "c\u0073harp", "for": "m\u0065thod", "severity": "bl\u006fck", "title": "T", "forbid": true } ] }"""
        };

        foreach(var (name, text) in accepted)
        {
            RulesFixture.Parse(text);
            Assert.True(schema.IsValid(text), $"The schema refuses {name}, which the reader accepts.");
        }
    }

    [Fact]
    public void FilesTheReaderRefusesFailTheSchema()
    {
        var schema = LoadSchema();
        var refused = new[]
        {
            """{ "rule": [] }""",
            """{ "rules": [ { "id": "A", "language": "csharp", "for": "method", "severity": "block", "title": "T", "forbid": true, "color": "red" } ] }""",
            Rule("""{ "for": "tree", "forbid": true }"""),
            Rule("""{ "for": "if-statement", "where": { "parameterCount": 1 }, "forbid": true }"""),
            Rule("""{ "where": { "lines": "40" }, "forbid": true }"""),
            Rule("""{ "where": { "name": { "atMost": 3 } }, "forbid": true }"""),
            Rule("""{ "where": { "isOverride": { "not": true } }, "forbid": true }"""),
            Rule("""{ "where": { "lines": { "matches": "4*" } }, "forbid": true }"""),
            Rule("""{ "where": { "lines": { "atLeast": 1, "not": 2 } }, "forbid": true }"""),
            Rule("""{ "where": { "lines": { "below": 4 } }, "forbid": true }"""),
            Rule("""{ "where": { "declaredAccess": "open" }, "forbid": true }"""),
            Rule("""{ "where": { "lines": 4.5 }, "forbid": true }"""),
            Rule("""{ "where": {}, "forbid": true }"""),
            Rule("""{ "where": { "lines": [] }, "forbid": true }"""),
            Rule("""{ "require": { "lines": 4 }, "forbid": true }"""),
            Rule("""{ "forbid": null }"""),
            Rule("""{ "forbid": false }"""),
            Rule("""{ "severity": "error", "forbid": true }"""),
            Rule("""{ "id": " ", "forbid": true }"""),
            Rule("""{ "title": null, "forbid": true }"""),
            """{ "requires": "v1", "rules": [] }""",
            """{ "requires": "1.2.3.4.5", "rules": [] }""",
            """{ "strictness": "loud", "rules": [] }""",
            """{ "strictness": { "error": "block", "warning": "warn" }, "rules": [] }""",
            """{ "rules": [], "structure": { "modules": { "web": ["assembly:Core"] } } }""",
            """{ "rules": [], "structure": { "modules": { "web": [] }, "directions": { "allow": ["web => web"] } } }""",
            """{ "rules": [], "structure": { "modules": {}, "layout": [1] } }""",
            """{ "rules": [], "structure": { "modules": {}, "references": [ { "from": null } ] } }""",
            """{ "rules": [], "structure": { "modules": {}, "layout": [ { "of": [ { "deep": null } ] } ] } }""",
            """{ "requires": "1.0.0.6\n", "rules": [] }""",
            """{ "rules": [], "structure": { "modules": { "web": ["namespace:W"], "app": ["namespace:A"] }, "directions": { "allow": ["web->->app"] } } }""",
            """{ "rules": [], "structure": { "modules": { "web": ["namespace:W"], "app": ["namespace:A"] }, "directions": { "forbid": ["web -> web"] } } }""",
            Rule("""{ "where": { "lines": 9223372036854775808 }, "forbid": true }"""),
            Rule("""{ "where": { "lines": { "atMost": -9223372036854775809 } }, "forbid": true }""")
        };

        foreach(var text in refused)
        {
            Assert.ThrowsAny<SettingsException>(() => RulesFixture.Parse(text));
            Assert.False(schema.IsValid(text), $"The schema accepts what the reader refuses: {text}");
        }
    }

    [Fact]
    public void ASchemaKeyIsReadKeptFirstAndAsksForTheVersionThatReadsIt()
    {
        using var named = new RulesFixture($$"""{ "$schema": "{{_address}}", "rules": [] }""");
        using var plain = new RulesFixture("""{ "rules": [] }""");
        Assert.Equal(_address, named.Settings().Schema);

        foreach(var fixture in new[] { named, plain })
            Assert.Equal(ExitCode.Passed, fixture.Commands.Run("rules", "add", "R", "--for", "method", "--title", "Rule", "--require", "lines=40").ExitCode);

        var lines = named.SettingsText().Split('\n');
        Assert.Equal($"    \"$schema\": \"{_address}\",", lines[1]);
        Assert.Equal("1.0.0.7", named.Settings().Requires);
        Assert.Equal(_address, named.Settings().Schema);
        Assert.Null(plain.Settings().Schema);
        Assert.Null(plain.Settings().Requires);

        // a save of the structure alone raises the version in the same way, and leaves the rest of the text as it was
        using var structured = new RulesFixture($$"""
            {
                "$schema": "{{_address}}",
                "rules": []
            }
            """);
        using var ranged = new RulesFixture("""
            {
                "requires": "1.0.0.1",
                "rules": [ { "id": "A", "language": "csharp", "for": "method", "severity": "block", "title": "T", "where": { "parameterCount": { "atLeast": 1 } }, "forbid": true } ]
            }
            """);
        foreach(var fixture in new[] { structured, ranged })
            Assert.Equal(ExitCode.Passed, fixture.Commands.Run("structure", "set", "add-module", "web").ExitCode);

        var structuredLines = structured.SettingsText().Split('\n');
        Assert.Equal($"    \"$schema\": \"{_address}\",", structuredLines[1]);
        Assert.Equal("    \"requires\": \"1.0.0.7\",", structuredLines[2]);
        Assert.Equal("    \"rules\": [],", structuredLines[3]);

        var rangedLines = ranged.SettingsText().Split('\n');
        Assert.Equal("    \"requires\": \"1.0.0.6\",", rangedLines[1]);
        Assert.Contains("\"parameterCount\": { \"atLeast\": 1 }", rangedLines[2], StringComparison.Ordinal);
    }

    private static string Rule(string overrides)
    {
        using var overridden = JsonDocument.Parse(overrides);
        var rule = new Dictionary<string, JsonElement>
        {
            ["id"] = JsonSerializer.SerializeToElement("A"),
            ["language"] = JsonSerializer.SerializeToElement("csharp"),
            ["for"] = JsonSerializer.SerializeToElement("method"),
            ["severity"] = JsonSerializer.SerializeToElement("block"),
            ["title"] = JsonSerializer.SerializeToElement("T")
        };
        foreach(var property in overridden.RootElement.EnumerateObject())
            rule[property.Name] = property.Value.Clone();

        return "{ \"rules\": [ " + JsonSerializer.Serialize(rule) + " ] }";
    }

    private static MiniSchemaValidator LoadSchema()
    {
        var text = SettingsSchemaGenerator.Generate("csharp", new CSharpArtifactCatalog());
        return new MiniSchemaValidator(JsonElement.Parse(text));
    }

    private static string RepositoryFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while(folder != null && !File.Exists(Path.Combine(folder.FullName, "CF.slnx")))
            folder = folder.Parent;

        return folder?.FullName ?? throw new InvalidOperationException("The repository's folder was not found above the tests.");
    }
}
