using System.Security.Cryptography;
using System.Text;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The settings of ours that every build of a review gets, the same each time: written once so that the build and the
/// key of the kept answers cannot disagree.
/// </summary>
/// <remarks>
/// The repository's own settings cannot switch our entries off, because analyzers are switched on again from outside,
/// and cannot raise them to errors, because the entries are notes that cannot be configured.
/// </remarks>
internal static class BuildSettings
{
    /// <summary>
    /// The arguments of every build, whatever the project: compile only that project, no compiler server, code style
    /// on, and analyzers on whatever the project says.
    /// </summary>
    public static IReadOnlyList<string> FixedArguments { get; } = new List<string>
    {
        "-nologo",
        "-v:q",
        "-m:1",
        "-nodeReuse:false",
        "-t:Build",
        "-p:UseSharedCompilation=false",
        "-p:BuildProjectReferences=false",
        "-p:EnforceCodeStyleInBuild=true",
        "-p:RunAnalyzers=true",
        "-p:RunAnalyzersDuringBuild=true"
    };

    /// <summary>
    /// A fingerprint of the file and the arguments above: answers kept under another fingerprint are not used.
    /// </summary>
    public static string Fingerprint()
    {
        var text = ToolchainTargets.TargetsText + "\n" + string.Join("\n", FixedArguments);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}