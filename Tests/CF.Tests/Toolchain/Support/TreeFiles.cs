using System.Text;

namespace CF.Tests.Toolchain.Support;

/// <summary>
/// Writes the files of a small tree into a folder, from paths relative to it.
/// </summary>
internal static class TreeFiles
{
    public const string SourceText = "namespace N;\n\npublic class C\n{\n}\n";

    public const string NoRules = "{ \"rules\": [] }\n";

    public const string BracedIfRule = """
        {
            "rules": [
                {
                    "id": "TEAM010",
                    "language": "csharp",
                    "for": "if-statement",
                    "severity": "block",
                    "title": "No braces around a single-line if",
                    "where": { "statementsInBody": 1, "bodyFitsOnOneLine": true },
                    "require": { "braces": false }
                }
            ]
        }

        """;

    public static void Write(string folder, string relativePath, string text)
    {
        var path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    /// <summary>
    /// Writes an empty project file for each project of the fake build, a plain source file for each source it compiles
    /// unless the tree holds one already, and the settings file.
    /// </summary>
    public static void WriteTree(string folder, IEnumerable<FakeProject> projects, string settings)
    {
        foreach(var project in projects)
        {
            Write(folder, project.Path, "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
            foreach(var source in project.Units.SelectMany(unit => unit.Compiles).Distinct())
            {
                if(!File.Exists(Path.Combine(folder, source)))
                    Write(folder, source, SourceText);
            }
        }

        Write(folder, "commitfence.json", settings);
    }
}
