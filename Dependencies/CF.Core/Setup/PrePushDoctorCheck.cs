using CF.Core.Ports;
using static CF.Core.Setup.DoctorTextPart;

namespace CF.Core.Setup;

/// <summary>Reads the active hooks without executing them.</summary>
internal sealed class PrePushDoctorCheck(IDoctorGit git, IFileSystem files)
    : DoctorCheck("Git and the hook", "The pre-push hook runs CommitFence first", "find the hook")
{
    private const string _stop = "|| exit $?";
    internal const string PreCommitTitle = "The pre-commit hook runs CommitFence first";
    private readonly HookScript _hooks = new HookScript(git, files);

    protected override DoctorCheckResult Check(DoctorContext context)
    {
        var edit = _hooks.Inspect(context.Repository, HookKind.PrePush);
        if(edit.ReviewIndex < 0)
        {
            var remedy = CheckMissingReview(context, edit);
            if(remedy != null)
                return remedy;
        }

        if(!files.FileExists(edit.GitPath))
            return NoHook(edit, new DoctorText("The repository has no pre-push hook."));

        if(!context.Machine.IsExecutable(edit.GitPath))
        {
            var name = Path.GetRelativePath(context.Repository.WorkFolder, edit.GitPath).Replace('\\', '/');
            return NoHook(edit, new DoctorText("Git cannot run ", CodeText(name), " because it is not executable."));
        }

        if(!files.FileExists(edit.Path))
            return NoHook(edit, new DoctorText("The Husky wrapper has no team hook at ", CodeText(edit.ShownPath), "."));

        return ReadHook(edit) with { HookPath = edit.Path };
    }

    private DoctorCheckResult? CheckMissingReview(DoctorContext context, HookEdit edit)
    {
        var managed = IsLefthook(edit) && CanRun(context, edit);
        var workFolder = context.Repository.WorkFolder;
        var path = Path.Combine(workFolder, LefthookAdvice.FileName);
        var configPath = managed && files.FileExists(path) ? path : null;
        int? jobIndent = null;
        var lefthook = configPath == null ? null : CheckLefthook(workFolder, configPath, out jobIndent);
        if(lefthook != null)
            return lefthook;

        var beforeCommit = _hooks.Inspect(context.Repository, HookKind.PreCommit);
        if(beforeCommit.ReviewIndex >= 0 && CanRun(context, beforeCommit))
            return ReadHook(beforeCommit) with { Title = PreCommitTitle };

        if(!managed)
            return null;

        return new DoctorCheckResult(Group, Title, DoctorOutcome.Fails, new DoctorText(LefthookAdvice.NotConfirmed),
            LefthookAdvice.Fix(edit.ShownPath, workFolder, configPath, jobIndent), DoctorImpact.NoReview);
    }

    private static bool IsLefthook(HookEdit edit)
    {
        return edit.ManualReason == null && edit.Manager == "lefthook";
    }

    private DoctorCheckResult? CheckLefthook(string workFolder, string path, out int? jobIndent)
    {
        var config = files.ReadAllText(path);
        var (hasEntry, indent) = LefthookAdvice.Inspect(config);
        jobIndent = indent;
        var file = CodeText(LefthookAdvice.FileName);
        if(LefthookAdvice.HasOtherStdin(config, hasEntry))
        {
            var found = new DoctorText("Another command in ", file, " has ", CodeText("use_stdin"), ", so the push input may go to that command instead of CommitFence.");
            return Fail(found, new DoctorText("Leave ", CodeText("use_stdin"), " only on CommitFence's pre-push entry."),
                DoctorImpact.HookOrderUncertain, [new DoctorAction(DoctorActionKind.OpenFile, "Open lefthook.yml", path)]);
        }

        var canVerify = hasEntry && !LefthookAdvice.HasOverrides(config)
            && !files.FileExists(Path.Combine(workFolder, "lefthook-local.yml")) && !files.FileExists(Path.Combine(workFolder, "lefthook-local.yaml"));
        if(!canVerify)
            return null;

        var result = Pass(new DoctorText(file, " configures CommitFence for pre-push with standard input."));
        return result with { Title = "The pre-push hook runs CommitFence through lefthook" };
    }

    private bool CanRun(DoctorContext context, HookEdit edit)
    {
        return files.FileExists(edit.GitPath) && context.Machine.IsExecutable(edit.GitPath);
    }

    private DoctorCheckResult NoHook(HookEdit edit, DoctorText found)
    {
        var path = files.FileExists(edit.Path) ? edit.Path : null;
        var actions = path != null ? ScriptActions(edit, null) : [CopyLine(edit)];
        var fix = edit.ManualReason != null
            ? ManualAdvice(edit)
            : edit.Updated != null && edit.Original == null
            ? new DoctorText("Run ", CodeText("commitfence init --hook pre-push"), " to create ", CodeText(edit.ShownPath), " with the CommitFence line.")
            : new DoctorText("Put the CommitFence line first in an executable hook at ", CodeText(edit.ShownPath), ".");

        return Fail(found, fix, DoctorImpact.NoReview, actions) with { HookPath = path };
    }

    private DoctorCheckResult ReadHook(HookEdit edit)
    {
        var shownPath = CodeText(edit.ShownPath);
        var review = edit.ReviewIndex;
        var verb = edit.Kind == HookKind.PrePush ? "push" : "commit";
        if(review < 0)
            return NoReviewLine(edit, shownPath);

        if(!edit.HasReviewLine)
        {
            return ScriptFailure(edit, review + 1,
                new DoctorText($"Line {review + 1} does not end in ", CodeText(_stop), $", so it does not explicitly stop a {verb} when CommitFence fails."),
                new DoctorText($"Replace the hook line with the line below so it can stop a {verb}. CommitFence does not change a hook script it did not write."),
                DoctorImpact.HookDoesNotStopPush);
        }

        var first = HookScript.FirstCommand(edit.Lines);
        if(review != first)
        {
            if(edit.Kind == HookKind.PreCommit)
            {
                var misplaced = new DoctorText($"It runs on line {review + 1} of ", shownPath, $". Line {first + 1} runs before it.");
                return ScriptFailure(edit, review + 1, misplaced, new DoctorText("Move the CommitFence line so it is the first command."), DoctorImpact.HookOrderUncertain);
            }

            return OutOfOrder(edit, shownPath, first, review);
        }

        return Pass(new DoctorText($"Line {review + 1} of ", shownPath, ", ending in ", CodeText(_stop), $", so it can stop a {verb}."));
    }

    private DoctorCheckResult NoReviewLine(HookEdit edit, DoctorTextPart shownPath)
    {
        var found = new DoctorText("No line of ", shownPath, " runs ", CodeText("CommitFence"), ".");
        if(edit.Updated != null)
        {
            var addition = DoctorExcerpt.HookInsert(edit);
            var script = edit.IsHusky ? "husky's" : "a plain shell hook";
            var fix = new DoctorText("Add the CommitFence line as the first command of ", shownPath,
                $". The script is {script}, so CommitFence can add it for you; nothing else in the script changes.");
            return new DoctorCheckResult(Group, Title, DoctorOutcome.Fails, found,
                new DoctorFix(fix, ScriptActions(edit, null), addition.Before) { Addition = addition }, DoctorImpact.NoReview);
        }

        var advice = edit.ManualReason != null ? ManualAdvice(edit) : edit.Manager switch
        {
            "pre-commit" => ManagedAdvice(edit, "pre-commit", ".pre-commit-config.yaml"),
            "overcommit" => ManagedAdvice(edit, "overcommit", ".overcommit.yml"),
            _ => new DoctorText("Add the CommitFence line as the first command of the hook. CommitFence does not recognise this script, so it leaves it to you.")
        };
        return ScriptFailure(edit, null, found, advice, DoctorImpact.NoReview);
    }

    private static DoctorText ManualAdvice(HookEdit edit)
    {
        var path = CodeText(edit.ShownPath);
        return edit.RunsGitLfs
            ? new DoctorText("To use the line below in ", path, ", arrange for both commands to get the hook input. ", edit.ManualReason!)
            : new DoctorText("Add the CommitFence line as the first command of ", path, ". ", edit.ManualReason!);
    }

    private static DoctorText ManagedAdvice(HookEdit edit, string manager, string config)
    {
        return new DoctorText($"Add the CommitFence line as the first command of the hook. {manager} writes ", CodeText(edit.ShownPath),
            " and would write over a line added there, so add it to ", CodeText(config), " instead.");
    }

    private DoctorCheckResult OutOfOrder(HookEdit edit, DoctorTextPart shownPath, int first, int review)
    {
        var prior = edit.Lines[first].Trim();
        var readsInput = prior == "git lfs pre-push" || prior == "git lfs pre-push \"$@\"";
        var consequence = readsInput
            ? ", reads the push's ref lines before it, so CommitFence gets none."
            : ", runs before it and may read the push's ref lines.";
        var found = new DoctorText($"It runs on line {review + 1} of ", shownPath, $". Line {first + 1}, ", CodeText(prior), consequence);
        var impact = readsInput ? DoctorImpact.HookOrder : DoctorImpact.HookOrderUncertain;
        if(edit.RunsGitLfs)
        {
            var current = DoctorExcerpt.Around(edit.Path, edit.Lines, review + 1, first + 1);
            return Fail(found, ManualAdvice(edit), impact, ScriptActions(edit, current.Line), current);
        }

        var move = first == 0 ? "Move the CommitFence line above line 1" : "Move the hook line up, under the first line";
        var fix = new DoctorText(move + ", so it reads the ref lines first. CommitFence does not change a hook script it did not write.");
        var (now, afterMove) = DoctorExcerpt.HookMove(edit.Path, edit.Lines, first + 1, review + 1);
        return Fail(found, fix, impact, ScriptActions(edit, now.Line), now, afterMove: afterMove);
    }

    private DoctorCheckResult ScriptFailure(HookEdit edit, int? line, DoctorText found, DoctorText fix, DoctorImpact impact)
    {
        return Fail(found, fix, impact, ScriptActions(edit, line), DoctorExcerpt.Around(edit.Path, edit.Lines, line));
    }

    private static List<DoctorAction> ScriptActions(HookEdit edit, int? line)
    {
        return [new DoctorAction(DoctorActionKind.OpenFile, "Open the hook script", edit.Path, line), CopyLine(edit)];
    }

    private static DoctorAction CopyLine(HookEdit edit)
    {
        return new DoctorAction(DoctorActionKind.CopyText, "Copy the hook line", HookLine.For(edit.Kind));
    }
}
