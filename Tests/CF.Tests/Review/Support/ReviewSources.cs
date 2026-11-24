using System.Text;

namespace CF.Tests.Review.Support;

/// <summary>
/// The rules and the C# that the review tests read: a small class a rule can be broken in, and the files of a project.
/// </summary>
internal static class ReviewSources
{
    public const string Team010 = """
        {
            "id": "TEAM010",
            "language": "csharp",
            "for": "if-statement",
            "severity": "block",
            "title": "No braces around a single-line if",
            "where": { "statementsInBody": 1, "bodyFitsOnOneLine": true },
            "require": { "braces": false }
        }
        """;

    public const string Team011 = """
        {
            "id": "TEAM011",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "Only public and private methods inside an internal class",
            "where": { "containingType": "class", "containingTypeDeclaredAccess": "internal" },
            "require": { "declaredAccess": ["public", "private"] }
        }
        """;

    public const string Team011Written = """
        {
            "id": "TEAM011S",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "Only public and private methods inside a class written internal",
            "where": { "containingType": "class", "containingTypeWrittenAccess": "internal" },
            "require": { "writtenAccess": ["public", "private"] }
        }
        """;

    public const string Documented = """
        {
            "id": "DOC1",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "A method has documentation",
            "require": { "documentationPresent": true }
        }
        """;

    public const string NoRun = """
        {
            "id": "EXT010",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "No method named Run",
            "where": { "name": "Run" },
            "forbid": true
        }
        """;

    public const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net11.0</TargetFramework>
            <EnableNETAnalyzers>false</EnableNETAnalyzers>
          </PropertyGroup>
        </Project>
        """;

    public const string SignBad = """
        public int Sign(int x)
        {
            if(x > 0)
            {
                return 1;
            }

            return 0;
        }
        """;

    public const string SignOk = """
        public int Sign(int x)
        {
            if(x > 0)
                return 1;

            return 0;
        }
        """;

    public const string Add = """
        public int Add(int a, int b)
        {
            return a + b;
        }
        """;

    public const string Twice = """
        public int Twice(int a)
        {
            return a * 2;
        }
        """;

    /// <summary>
    /// A settings file with these rules (each one a piece of JSON).
    /// </summary>
    public static string Settings(params string[] rules)
    {
        return "{ \"rules\": [" + string.Join(",", rules) + "] }" + "\n";
    }

    /// <summary>
    /// A public class in the namespace Demo with the methods, each one indented as a member.
    /// </summary>
    public static string Class(string name, params string[] methods)
    {
        return ClassWith("public class " + name, methods);
    }

    public static string ClassWith(string header, params string[] methods)
    {
        var text = new StringBuilder();
        text.Append("namespace Demo;\n\n").Append(header).Append("\n{\n");
        for(var index = 0; index < methods.Length; index++)
        {
            if(index > 0)
                text.Append('\n');

            foreach(var line in methods[index].ReplaceLineEndings("\n").Split('\n'))
                text.Append(line.Length == 0 ? string.Empty : "    " + line).Append('\n');
        }

        return text.Append("}\n").ToString();
    }

    /// <summary>
    /// The files of a small project: its project file and the class.
    /// </summary>
    public static Dictionary<string, string> Project(params (string Path, string Text)[] files)
    {
        var all = new Dictionary<string, string>(StringComparer.Ordinal) { ["Demo.csproj"] = ProjectFile + "\n" };
        foreach(var file in files)
            all[file.Path] = file.Text;

        return all;
    }

    /// <summary>
    /// The line, counted from 1, of the first line that holds the text.
    /// </summary>
    public static int LineOf(string source, string text)
    {
        var lines = source.ReplaceLineEndings("\n").Split('\n');
        var index = Array.FindIndex(lines, line => line.Contains(text, StringComparison.Ordinal));
        return index < 0 ? throw new InvalidOperationException($"'{text}' is not in the source") : index + 1;
    }
}
