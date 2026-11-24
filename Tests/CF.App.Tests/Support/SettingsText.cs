namespace CF.App.Tests.Support;

/// <summary>
/// The text of the settings files the tests start from.
/// </summary>
internal static class SettingsText
{
    /// <summary>
    /// Four rules: two block (one of them off), two warn. The ids are in the order of the file.
    /// </summary>
    public const string Four = """
        {
            "rules": [
                {
                    "id": "TEAM001",
                    "language": "csharp",
                    "for": "if-statement",
                    "severity": "block",
                    "title": "No braces around a single-line if",
                    "where": { "statementsInBody": 1, "bodyFitsOnOneLine": true },
                    "require": { "braces": false }
                },
                {
                    "id": "TEAM002",
                    "language": "csharp",
                    "for": "method",
                    "severity": "warn",
                    "title": "Methods are documented",
                    "require": { "documentationPresent": true },
                    "message": "A method needs a documentation comment."
                },
                {
                    "id": "TEAM003",
                    "language": "csharp",
                    "for": "class",
                    "severity": "block",
                    "enabled": false,
                    "title": "Classes are documented",
                    "require": { "documentationPresent": true }
                },
                {
                    "id": "TEAM004",
                    "language": "csharp",
                    "for": "class",
                    "severity": "warn",
                    "title": "No partial classes",
                    "where": { "isPartial": true },
                    "forbid": true
                }
            ]
        }
        """;

    /// <summary>
    /// A file that is not JSON.
    /// </summary>
    public const string Malformed = "{ \"rules\": [ not json";

    /// <summary>
    /// Four rules as <see cref="Four" />, with another title on the first.
    /// </summary>
    public static string FourWithFirstTitled(string title)
    {
        return Four.Replace("No braces around a single-line if", title, StringComparison.Ordinal);
    }
}
