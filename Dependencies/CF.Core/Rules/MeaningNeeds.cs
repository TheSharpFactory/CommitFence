using CF.Core.Code;

namespace CF.Core.Rules;

/// <summary>
/// Tells the rules that run on syntax alone from the rules that need meaning: a rule needs
/// meaning when it is for an artifact that only meaning knows, or when any condition asks about a property whose value
/// comes from the compiler.
/// </summary>
internal static class MeaningNeeds
{
    public static bool Has(RuleDefinition rule, ILanguage language)
    {
        var artifactType = language.Catalog.FindArtifactType(rule.ArtifactType);
        if(artifactType == null)
            return false;

        if(artifactType.KnownOnlyFromMeaning)
            return true;

        return rule.Where.Concat(rule.Require).Any(condition => artifactType.FindProperty(condition.PropertyName)?.Kind == PropertyKind.Meaning);
    }
}