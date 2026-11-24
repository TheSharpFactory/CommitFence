namespace CF.App.Rules.Page;

/// <summary>
/// The words a rule's sentence is made of, said once: the page of a rule and the open finding of a review draw the same
/// sentence from them.
/// </summary>
internal static class SentenceWords
{
    public static string Artifact(string? artifactName)
    {
        return artifactName == null ? "the artifact" : RuleCatalog.Words(artifactName);
    }

    public static string Property(string? propertyName)
    {
        return propertyName == null ? "choose a property" : RuleCatalog.Words(propertyName);
    }

    public static string Comparison(bool isOneOf)
    {
        return isOneOf ? "is one of" : "is";
    }

    public static string Value(IReadOnlyList<string> values)
    {
        return string.Join(' ', values);
    }
}
