namespace CF.App.Rules.Page;

/// <summary>
/// What the path picker says about a pattern a person typed: the pattern kept as it is, or the reason it is refused. A pattern is
/// written from the root of the repository, with / between folders; only *, ** and ? are wildcards.
/// </summary>
internal static class PathPattern
{
    private const char _backslash = '\\';

    /// <summary>
    /// Why the pattern is refused, or nothing when it can be kept. The repository's name is used in the words.
    /// </summary>
    public static string? Problem(string pattern, string repositoryName)
    {
        if(pattern.Split('/', _backslash).Contains(".."))
            return $"A path stays inside {repositoryName}, so .. cannot be part of it.";

        if(pattern.IndexOfAny(['[', ']', '{', '}']) >= 0)
            return "Only *, ** and ? are wildcards here.";

        if(pattern.Contains(_backslash) || StartsAtARoot(pattern))
            return $"Write the path from the root of {repositoryName}, with / between folders: {Suggest(pattern, repositoryName)}.";

        return null;
    }

    /// <summary>
    /// The pattern with its separators turned, and a drive or the repository's own folder taken off the front.
    /// </summary>
    public static string Suggest(string pattern, string repositoryName)
    {
        var turned = pattern.Replace(_backslash, '/');
        if(turned.Length >= 2 && char.IsAsciiLetter(turned[0]) && turned[1] == ':')
            turned = turned[2..];

        turned = turned.TrimStart('/');
        var own = repositoryName + "/";
        return turned.StartsWith(own, StringComparison.OrdinalIgnoreCase) ? turned[own.Length..] : turned;
    }

    private static bool StartsAtARoot(string pattern)
    {
        return pattern.StartsWith('/') || (pattern.Length >= 2 && char.IsAsciiLetter(pattern[0]) && pattern[1] == ':');
    }
}
