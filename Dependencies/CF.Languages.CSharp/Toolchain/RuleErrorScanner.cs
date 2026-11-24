using System.Text.Json;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Reads the log of a build that failed and says which rules, if any, were the only reason: the error results whose
/// identifier is not the compiler's own (those starting with CS).
/// </summary>
/// <remarks>
/// A log that holds a compiler error, or no rule error, or a rule whose identifier is not a plain one, or that cannot
/// be read, gives none, and the build stays failed as it is.
/// </remarks>
internal sealed class RuleErrorScanner
{
    private const string _compilerPrefix = "CS";

    private readonly SarifReader _sarifReader;

    public RuleErrorScanner(SarifReader sarifReader)
    {
        _sarifReader = sarifReader;
    }

    public IReadOnlyList<string> FindRuleErrorIds(string logPath)
    {
        var errors = ReadErrors(logPath);
        if(errors.Any(IsCompilerError))
            return new List<string>();

        var ids = errors.Select(result => result.RuleId ?? string.Empty).Distinct().ToList();
        return ids.All(IsPlainIdentifier) ? ids : new List<string>();
    }

    /// <summary>
    /// An identifier goes into the build's command line, so only one made of letters, digits and underscores is ever
    /// let through; with any other the build stays failed.
    /// </summary>
    private static bool IsPlainIdentifier(string id)
    {
        return id.Length > 0 && id.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    }

    private List<SarifResultDto> ReadErrors(string logPath)
    {
        try
        {
            var run = _sarifReader.Read(logPath);
            return (run.Results ?? new List<SarifResultDto>()).Where(IsError).ToList();
        }
        catch(Exception exception) when(exception is JsonException || exception is IOException)
        {
            return new List<SarifResultDto>();
        }
    }

    private static bool IsError(SarifResultDto result)
    {
        var suppressed = result.Suppressions != null && result.Suppressions.Count > 0;
        return result.Level == "error" && result.RuleId != null && !SarifMeaningNotes.IsMeaningRule(result.RuleId) && !suppressed;
    }

    private static bool IsCompilerError(SarifResultDto result)
    {
        return result.RuleId != null && result.RuleId.StartsWith(_compilerPrefix, StringComparison.Ordinal);
    }
}