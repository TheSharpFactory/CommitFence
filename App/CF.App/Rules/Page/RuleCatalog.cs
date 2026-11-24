using System.Text;
using CF.Core.Code;
using CF.Core.Rules;

namespace CF.App.Rules.Page;

/// <summary>
/// What the languages offer to rules, as the page's pickers need it: the languages, the artifact types of a language, the
/// properties of an artifact type, and the words a person reads for each. It is filled from the rule service's catalog and
/// nothing else, so a picker offers exactly what <c>commitfence catalog</c> prints.
/// </summary>
internal sealed class RuleCatalog
{
    public RuleCatalog(IReadOnlyList<LanguageCatalog> languages)
    {
        Languages = languages;
    }

    public IReadOnlyList<LanguageCatalog> Languages { get; }

    /// <summary>
    /// The language a new rule is for when the product has one: it is chosen, not asked for.
    /// </summary>
    public string DefaultLanguageId
    {
        get
        {
            return Languages.Count == 0 ? string.Empty : Languages[0].Id;
        }
    }

    /// <summary>
    /// A name a person reads for a name the rules use: <c>statementsInBody</c> is "statements in body", <c>if-statement</c>
    /// is "if statement".
    /// </summary>
    public static string Words(string name)
    {
        return RuleWords.Of(name);
    }

    /// <summary>
    /// What kind of value a property takes, as the picker says it.
    /// </summary>
    public static string ValueKind(PropertyDescriptor property)
    {
        if(property.ValueType == PropertyValueType.Number)
            return "number";

        if(property.ValueType == PropertyValueType.Boolean)
            return "true or false";

        return property.AllowedValues.Count > 0 ? "one of a list" : "text";
    }

    public string LanguageName(string languageId)
    {
        return FindLanguage(languageId)?.Name ?? languageId;
    }

    public LanguageCatalog? FindLanguage(string languageId)
    {
        return Languages.FirstOrDefault(language => language.Id == languageId);
    }

    public ArtifactType? FindArtifact(string languageId, string? artifactName)
    {
        if(artifactName == null)
            return null;

        return FindLanguage(languageId)?.ArtifactTypes.FirstOrDefault(type => type.Name == artifactName);
    }

    public PropertyDescriptor? FindProperty(string languageId, string? artifactName, string? propertyName)
    {
        if(propertyName == null)
            return null;

        return FindArtifact(languageId, artifactName)?.FindProperty(propertyName);
    }
}
