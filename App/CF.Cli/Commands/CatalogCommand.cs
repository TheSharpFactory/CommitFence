using CF.Core.Code;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Prints the artifact types of the languages and their properties, with the kind of value each takes and whether it
/// needs the build.
/// </summary>
internal sealed class CatalogCommand
{
    private readonly IRuleService _ruleService;
    private readonly TextWriter _output;
    private readonly string? _languageId;
    private readonly string? _artifactTypeName;

    public CatalogCommand(IRuleService ruleService, TextWriter output, string? languageId, string? artifactTypeName)
    {
        _ruleService = ruleService;
        _output = output;
        _languageId = languageId;
        _artifactTypeName = artifactTypeName;
    }

    public ExitCode Run()
    {
        foreach(var catalog in _ruleService.ListCatalogs(_languageId, _artifactTypeName))
        {
            _output.WriteLine($"{catalog.Id}  {catalog.Name}");
            foreach(var artifactType in catalog.ArtifactTypes)
                PrintArtifactType(artifactType);
        }

        return ExitCode.Passed;
    }

    private void PrintArtifactType(ArtifactType artifactType)
    {
        var note = artifactType.KnownOnlyFromMeaning ? " (needs the build)" : string.Empty;
        _output.WriteLine($"  {artifactType.Name}{note}: {artifactType.Description}");
        var width = artifactType.Properties.Max(property => property.Name.Length);
        foreach(var property in artifactType.Properties)
        {
            var comparisons = RuleComparisons.For(property.ValueType).Select(comparison => RuleSentence.ComparisonWords(comparison));
            _output.WriteLine($"    {property.Name.PadRight(width)}  {FormatValueType(property),-13}  {FormatKind(property),-14}  {property.Description} Comparisons: {string.Join(", ", comparisons)}.");
        }
    }

    private static string FormatValueType(PropertyDescriptor property)
    {
        if(property.ValueType == PropertyValueType.Number)
            return "number";

        return property.ValueType == PropertyValueType.Boolean ? "true or false" : "text";
    }

    private static string FormatKind(PropertyDescriptor property)
    {
        return property.Kind == PropertyKind.Meaning ? "needs the build" : "reads the file";
    }
}