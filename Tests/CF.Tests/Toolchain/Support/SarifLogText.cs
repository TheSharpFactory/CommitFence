using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CF.Tests.Toolchain.Support;

/// <summary>
/// Writes the text of a compiler's error log (SARIF) the way the compiler and the CommitFence analyzer write it, so that
/// a test can say what a build found without running one.
/// </summary>
internal sealed class SarifLogText
{
    private readonly List<object> _rules = new List<object>();
    private readonly List<object> _results = new List<object>();

    /// <summary>
    /// A log with nothing in it but the closing entry of the analyzer, as a build of clean code writes it.
    /// </summary>
    public static string CleanLog(string assembly, int sourceFiles)
    {
        return new SarifLogText().Closing(assembly, sourceFiles).ToString();
    }

    /// <summary>
    /// The closing entry of the analyzer for a unit without facts: the number of files it looked at and the checksum of no
    /// facts.
    /// </summary>
    public static string ClosingMessage(string assembly, int filesAnalyzed)
    {
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Empty))).ToLowerInvariant();
        return string.Join("|", "END", "1", assembly, filesAnalyzed.ToString(CultureInfo.InvariantCulture), "0", checksum);
    }

    /// <summary>
    /// Describes a rule: its default level and whether the compiler itself owns it.
    /// </summary>
    public SarifLogText Rule(string id, string defaultLevel, bool isCompilerRule)
    {
        _rules.Add(new { id, defaultConfiguration = new { level = defaultLevel }, properties = new { tags = isCompilerRule ? new[] { "Compiler" } : new[] { "Style" } } });
        return this;
    }

    /// <summary>
    /// A result of a rule at a place in a file, given by its full path, or without a place.
    /// </summary>
    public SarifLogText Result(string ruleId, string level, string message, string? fullPath, int line, int column, bool suppressed = false)
    {
        var locations = fullPath == null ? null : new[] { new { physicalLocation = new { artifactLocation = new { uri = new Uri(fullPath).AbsoluteUri }, region = new { startLine = line, startColumn = column, endLine = line, endColumn = column + 1 } } } };
        var suppressions = suppressed ? new[] { new { kind = "inSource" } } : null;
        _results.Add(new { ruleId, level, message = new { text = message }, locations, suppressions });
        return this;
    }

    public SarifLogText Closing(string assembly, int filesAnalyzed)
    {
        _results.Add(new { ruleId = "CFM999", level = "note", message = new { text = ClosingMessage(assembly, filesAnalyzed) } });
        return this;
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(new { version = "2.1.0", runs = new[] { new { tool = new { driver = new { rules = _rules } }, results = _results } } });
    }
}
