using CF.Core.Code;
using CF.Core.Findings;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Turns the results of one compiler log into findings.
/// </summary>
/// <remarks>
/// A compile error is a diagnostic of the compiler that is an error without any configuration: it blocks wherever it is
/// and is never filtered by lines. Every other diagnostic is a rule of the compiler or of the .editorconfig and carries
/// the lines it covers, so that only those the push changed are kept: error blocks, warning and suggestion warn. A
/// suppressed diagnostic is not a finding.
/// </remarks>
internal sealed class SarifFindingBuilder
{
    private const string _languageId = "csharp";

    private readonly string _treeFolder;

    public SarifFindingBuilder(string treeFolder)
    {
        _treeFolder = treeFolder;
    }

    public IReadOnlyList<Finding> Build(SarifRunDto run, ProjectUnit unit)
    {
        var rules = (run.Tool?.Driver?.Rules ?? new List<SarifRuleDto>()).Where(rule => rule.Id != null).GroupBy(rule => rule.Id!).ToDictionary(group => group.Key, group => group.First());
        var findings = new List<Finding>();
        foreach(var result in run.Results ?? new List<SarifResultDto>())
        {
            if(IsFinding(result))
                findings.Add(ToFinding(result, rules, unit));
        }

        return findings;
    }

    private static bool IsFinding(SarifResultDto result)
    {
        if(result.Suppressions != null && result.Suppressions.Count > 0)
            return false;

        return result.RuleId != null && !SarifMeaningNotes.IsMeaningRule(result.RuleId) && (result.Level == "error" || result.Level == "warning" || result.Level == "note");
    }

    private Finding ToFinding(SarifResultDto result, Dictionary<string, SarifRuleDto> rules, ProjectUnit unit)
    {
        var ruleId = result.RuleId ?? string.Empty;
        rules.TryGetValue(ruleId, out var rule);
        var isCompilerRule = rule?.Properties?.Tags != null && rule.Properties.Tags.Contains("Compiler");
        var isCompileError = isCompilerRule && result.Level == "error" && rule?.DefaultConfiguration?.Level == "error";
        var region = result.Locations?.FirstOrDefault()?.PhysicalLocation?.Region;
        var file = FindFile(result);
        var place = file != null && region != null && region.StartLine > 0;
        var severity = result.Level == "error" ? Severity.Block : Severity.Warn;
        var source = isCompilerRule ? FindingSource.Compiler : FindingSource.EditorConfig;
        var reach = place && !isCompileError ? ArtifactReach.OfSpan(new SourceSpan(region!.StartLine, region.StartColumn, Math.Max(region.EndLine, region.StartLine), region.EndColumn)) : null;

        return new Finding(ruleId, source, severity, _languageId, unit.Project.DisplayName, unit.Framework, file, place ? region!.StartLine : null, place ? region!.StartColumn : null, result.Message?.Text ?? string.Empty, null, reach);
    }

    private string? FindFile(SarifResultDto result)
    {
        var uri = result.Locations?.FirstOrDefault()?.PhysicalLocation?.ArtifactLocation?.Uri;
        if(string.IsNullOrEmpty(uri) || !Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || !parsed.IsFile)
            return null;

        return ToolchainPaths.RelativeTo(_treeFolder, parsed.LocalPath);
    }
}