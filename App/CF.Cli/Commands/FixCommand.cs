using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Fixes;
using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>Previews or applies a fix within the selected review scope.</summary>
internal sealed class FixCommand(IReviewService review, IFixService fixes, CommandEnvironment environment)
{
    public ExitCode Run(string[] args)
    {
        if(args.Length < 3 || args.Skip(1).Take(2).Any(value => string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal)))
            return BadUsage("fix needs a rule and a file.");

        var options = args.Skip(3).ToList();
        var apply = options.Remove("--apply");
        if(options.Contains("--apply") || options.Contains("--format") || options.Contains("--hook") || options.Contains("--files"))
            return BadUsage("fix accepts --apply once and reviews a push, the staged changes, a range or a folder.");

        if(!options.Any(option => option is "--push" or "--staged" or "--base" or "--all"))
            options.Add("--push");

        var parsed = ReviewArguments.Parse(["review", .. options], allowPushHead: true);
        if(parsed.Arguments == null)
            return BadUsage(parsed.Error);

        try
        {
            return RunFix(args[1], Path.GetFullPath(args[2], environment.CurrentFolder), parsed.Arguments, apply);
        }
        catch(Exception exception) when(exception is SettingsException or ReviewTargetException or ArgumentException)
        {
            return BadUsage(exception.Message);
        }
    }

    private ExitCode RunFix(string rule, string path, ReviewArguments arguments, bool apply)
    {
        var push = arguments.Mode == ReviewMode.Push ? review.FindPushScope(environment.CurrentFolder) : null;
        var scope = ScopeOf(arguments, push);
        if(scope == null)
            return NoFix(push?.Reason);

        if(arguments.Mode == ReviewMode.Push && arguments.HeadRevision.Length > 0)
        {
            var head = arguments.HeadRevision == "HEAD" ? push!.Branch : arguments.HeadRevision;
            scope = SelectHead(scope, head);
            if(scope == null)
                return NoFix("Choose one of the pushed refs with --head, or choose a range with --base and --head.");
        }

        var settings = arguments.SettingsPath == null ? null : Path.GetFullPath(arguments.SettingsPath, environment.CurrentFolder);
        var result = review.ReviewChanges(environment.CurrentFolder, scope, settings);
        if(result.Repository == null)
            return NoFix(result.Coverage.FirstOrDefault(entry => entry.Status == CoverageStatus.Failed)?.Reason);

        var file = Path.GetRelativePath(result.Repository.WorkFolder, path).Replace('\\', '/');
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var findings = result.Findings.Where(finding => finding.RuleId == rule && string.Equals(finding.File, file, comparison))
            .SelectMany(finding => result.TreesOf(finding).Select(tree => (Finding: finding, Tree: tree))).ToList();
        if(findings.Count == 0)
            return NoFix();

        if(findings.Any(entry => entry.Tree != findings[0].Tree))
            return NoFix("The finding occurs in more than one reviewed tree. Choose a pushed ref with --head, or a range with --base and --head.");

        var answer = fixes.SuggestFix(new FixRequest(result.Repository, findings[0].Tree, findings[0].Finding));
        if(answer.Fix == null)
            return NoFix(answer.Reason);

        if(!apply)
        {
            if(answer.Fix.Patch.Length > 0)
                environment.Output.WriteLine(answer.Fix.DescribeReach(arguments.Mode == ReviewMode.All));

            if(answer.Fix.ByteChanges.Length > 0)
                environment.Output.WriteLine(answer.Fix.ByteChanges);
            environment.Output.Write(answer.Fix.Patch);
            return ExitCode.Passed;
        }

        var applied = fixes.ApplyFix(answer.Fix);
        if(!applied.Succeeded)
        {
            environment.Error.WriteLine(applied.Reason);
            return ExitCode.Blocked;
        }

        environment.Output.WriteLine($"Applied the fix to {answer.Fix.File}. Review again before you push.");
        return ExitCode.Passed;
    }

    private static ReviewScope? SelectHead(ReviewScope scope, string head)
    {
        var refs = scope.PushedRefs.Refs.Where(pushed => !pushed.IsDeletion
            && (pushed.LocalRef == head || pushed.LocalRef == "refs/heads/" + head || pushed.LocalObject == head)).ToList();
        return refs.Count == 0 ? null
            : ReviewScope.ForPush(scope.RemoteName, scope.RemoteUrl, PushedRefs.FromRefs(refs)).WithSource(scope.Source);
    }

    private ReviewScope? ScopeOf(ReviewArguments arguments, PushScopeAnswer? push)
    {
        return arguments.Mode switch
        {
            ReviewMode.Staged => ReviewScope.ForStaged(),
            ReviewMode.Range => ReviewScope.ForRange(arguments.BaseRevision, arguments.HeadRevision),
            ReviewMode.All => ReviewScope.ForWholeFolder(arguments.Target.Length == 0 ? string.Empty : Path.GetFullPath(arguments.Target, environment.CurrentFolder)),
            ReviewMode.Push => push?.Scope,
            ReviewMode.Files or ReviewMode.Hook => null,
            _ => throw new ArgumentOutOfRangeException(nameof(arguments))
        };
    }

    private ExitCode NoFix(string? reason = null)
    {
        environment.Error.WriteLine(new FixAnswer(Reason: reason).NoFixText);
        return ExitCode.Blocked;
    }

    private ExitCode BadUsage(string? reason)
    {
        environment.Error.WriteLine(reason);
        environment.Error.WriteLine(UsageText.Text);
        return ExitCode.BadSettingsOrUsage;
    }
}
