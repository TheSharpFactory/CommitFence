using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Fixes;
using CF.Languages.CSharp.Toolchain;

namespace CF.Languages.CSharp.Fixer;

/// <summary>Runs the C# formatter on a single reviewed file and rule.</summary>
internal sealed class CSharpFixer(DotnetTool dotnet) : IFixer
{
    private static readonly TimeSpan _timeLimit = TimeSpan.FromSeconds(60);

    public FixAttempt Change(string treeFolder, Finding finding)
    {
        if(finding.Source != FindingSource.EditorConfig || finding.ProjectFile == null || finding.File == null || IsNumberedRule(finding.RuleId, "CS"))
            return new FixAttempt(false);

        var command = CommandFor(finding.RuleId);
        var arguments = new List<string> { "format", command, finding.ProjectFile, "--no-restore", "--include", finding.File };
        if(command != "whitespace")
            arguments.AddRange(["--diagnostics", finding.RuleId, "--severity", "info"]);

        if(finding.BuildFramework != null)
            arguments.AddRange(["--framework", finding.BuildFramework]);

        var result = dotnet.Run(treeFolder, arguments, _timeLimit);
        if(result.TimedOut)
            return new FixAttempt(true, "The formatter did not finish within 60 seconds.");

        if(result.ExitCode != 0)
            return new FixAttempt(true, DotnetTool.SummarizeErrors(result));

        if((result.Output + result.Error).Contains("Warnings were encountered while loading the workspace", StringComparison.Ordinal))
            return new FixAttempt(true, "The formatter could not load the project without warnings.");

        return new FixAttempt(true);
    }

    private static bool IsNumberedRule(string id, string prefix)
    {
        return id.Length > prefix.Length && id.StartsWith(prefix, StringComparison.Ordinal)
            && !id.AsSpan(prefix.Length).ContainsAnyExceptInRange('0', '9');
    }

    private static string CommandFor(string ruleId)
    {
        if(ruleId == "IDE0055")
            return "whitespace";

        return IsNumberedRule(ruleId, "IDE") ? "style" : "analyzers";
    }
}
