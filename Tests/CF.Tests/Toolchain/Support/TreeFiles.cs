using System.Text;
using System.Xml.Linq;

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
    /// Writes a project file with the inputs the fake build reports, a plain source file for each source it compiles
    /// unless the tree holds one already, and the settings file.
    /// </summary>
    public static void WriteTree(string folder, IEnumerable<FakeProject> projects, string settings)
    {
        foreach(var project in projects)
        {
            WriteProject(folder, project);
            foreach(var source in project.Units.SelectMany(unit => unit.Compiles).Distinct())
            {
                if(!File.Exists(Path.Combine(folder, source)))
                    Write(folder, source, SourceText);
            }
        }

        Write(folder, "commitfence.json", settings);
    }

    private static void WriteProject(string folder, FakeProject project)
    {
        var projectFolder = Path.GetDirectoryName(Path.Combine(folder, project.Path))!;
        var items = new XElement("ItemGroup");
        foreach(var reference in project.Units.SelectMany(unit => unit.References).Distinct())
            items.Add(new XElement("ProjectReference", new XAttribute("Include", Path.GetRelativePath(projectFolder, Path.Combine(folder, reference)))));

        foreach(var source in project.Units.SelectMany(unit => unit.Compiles).Distinct())
            items.Add(new XElement("Compile", new XAttribute("Include", Path.GetRelativePath(projectFolder, Path.Combine(folder, source)))));

        var document = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), items);
        Write(folder, project.Path, document.ToString());
    }
}
