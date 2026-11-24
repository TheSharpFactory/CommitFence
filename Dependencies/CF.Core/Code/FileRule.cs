using CF.Core.Findings;

namespace CF.Core.Code;

/// <summary>
/// A rule that a file of the repository itself sets for a language's tools, such as a line of an .editorconfig.
/// </summary>
public sealed class FileRule
{
    /// <summary>
    /// Creates a rule of a file.
    /// </summary>
    public FileRule(string id, string? title, string setting, FileRuleState state, Severity? severity, string file, int line, IReadOnlyList<FileLine>? lines = null)
        : this(id, title, file, "C#", new FileRulePlace("*.cs", setting, state, severity, line, lines ?? [], _ => true), [])
    {
    }

    /// <summary>Creates a scoped rule from the places that set it.</summary>
    public FileRule(string id, string? title, string file, string language, FileRulePlace? main, IReadOnlyList<FileRulePlace> exceptions)
    {
        Id = id;
        Title = title;
        File = file;
        Language = language;
        Main = main;
        Exceptions = exceptions;
    }

    /// <summary>The language as shown to the reader.</summary>
    public string Language { get; }

    /// <summary>The last setting for the language as a whole.</summary>
    public FileRulePlace? Main { get; }

    /// <summary>The narrower settings in file order.</summary>
    public IReadOnlyList<FileRulePlace> Exceptions { get; }

    /// <summary>All places in source order.</summary>
    public IEnumerable<FileRulePlace> Places
    {
        get
        {
            return Main == null ? Exceptions : Exceptions.Prepend(Main);
        }
    }

    /// <summary>Finds the setting for a repository-relative path.</summary>
    public FileRulePlace? ForPath(string path)
    {
        var relative = path.Replace('\\', '/');
        return Exceptions.LastOrDefault(place => place.Matches(relative)) ?? (Main?.Matches(relative) == true ? Main : null);
    }

    /// <summary>The scope of a place in this rule.</summary>
    public string Scope(FileRulePlace place)
    {
        return place == Main ? $"all {Language} files" : place.Pattern;
    }

    /// <summary>The number of scoped overrides in words.</summary>
    public string ExceptionCount
    {
        get
        {
            return $"{Exceptions.Count} {(Exceptions.Count == 1 ? "exception" : "exceptions")}";
        }
    }

    /// <summary>Groups narrower settings in the order used by the effect filter.</summary>
    public IEnumerable<IGrouping<string, FileRulePlace>> ExceptionGroups()
    {
        return Exceptions.GroupBy(place => place.Effect).OrderBy(group => group.Key switch { "Blocks" => 0, "Warns" => 1, _ => 2 });
    }

    /// <summary>One list line for an effect with all its locations.</summary>
    public static string DescribeGroup(IGrouping<string, FileRulePlace> group)
    {
        var parts = group.Select(place => $"{place.Pattern}, line {place.Line}").ToList();
        if(parts.Count > 1)
            parts[^1] = "and " + parts[^1];

        return $"{group.Key} for {string.Join(", ", parts)}";
    }

    /// <summary>The exception summaries shared by the list views.</summary>
    public IEnumerable<string> ExceptionLines()
    {
        return ExceptionGroups().Select(DescribeGroup);
    }

    /// <summary>
    /// The rule's identifier, as the file writes it.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// What the rule is about, or nothing when neither the language's tools nor the file say.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// The setting as the file writes it, without a comment.
    /// </summary>
    public string Setting
    {
        get
        {
            return Main?.Setting ?? string.Empty;
        }
    }

    /// <summary>
    /// Whether the build applies the rule.
    /// </summary>
    public FileRuleState State
    {
        get
        {
            return Main?.State ?? FileRuleState.OwnDefault;
        }
    }

    /// <summary>
    /// What a breach does to a push when the rule is on: block or warn. Nothing when the rule is not on.
    /// </summary>
    public Severity? Severity
    {
        get
        {
            return Main?.Severity;
        }
    }

    /// <summary>
    /// The file that sets the rule, as people name it.
    /// </summary>
    public string File { get; }

    /// <summary>
    /// The line of the file, counted from 1.
    /// </summary>
    public int? Line
    {
        get
        {
            return Main?.Line;
        }
    }

    /// <summary>
    /// The quoted source lines of the main setting, if present.
    /// </summary>
    public IReadOnlyList<FileLine> Lines
    {
        get
        {
            return Main?.Lines ?? [];
        }
    }
}