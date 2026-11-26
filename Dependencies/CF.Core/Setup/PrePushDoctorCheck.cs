using System.Text.RegularExpressions;
using CF.Core.Ports;
using static CF.Core.Setup.DoctorTextPart;

namespace CF.Core.Setup;

/// <summary>Reads the active pre-push script without executing it.</summary>
internal sealed partial class PrePushDoctorCheck(IDoctorGit git, IFileSystem files)
    : DoctorCheck("Git and the hook", "The pre-push hook runs CommitFence first", "find the hook")
{
    private const string _stop = "|| exit $?";

    [GeneratedRegex("""^"\$\(\s*git[ \t]+config[ \t]+--get[ \t]+commitfence\.path\s*\)"[ \t]+review[ \t]+--hook[ \t]+pre-push[ \t]+"\$@"(?=$|[ \t;&|<>])""")]
    private static partial Regex ReviewLine();

    [GeneratedRegex("""^[ \t]*\|\|[ \t]*exit[ \t]+\$\?(?:[ \t]+#.*)?$""")]
    private static partial Regex StopsPush();

    protected override DoctorCheckResult Check(DoctorContext context)
    {
        var path = git.FindPrePushHook(context.Repository);
        if(!files.FileExists(path))
            return NoHook(path, new DoctorText("The repository has no pre-push hook."));

        if(!context.Machine.IsExecutable(path))
            return NoHook(path, new DoctorText("Git cannot run ", CodeText(ShownPath(context, path)), " because it is not executable."));

        var lines = DoctorExcerpt.ReadLines(files.ReadAllText(path));
        var first = FindLine(lines, line => line.Length != 0 && !line.StartsWith('#'));
        var review = FindLine(lines, line => ReviewLine().IsMatch(line));
        return ReadHook(context, path, lines, first, review);
    }

    private DoctorCheckResult NoHook(string path, DoctorText found)
    {
        var actions = files.FileExists(path)
            ? new List<DoctorAction> { new DoctorAction(DoctorActionKind.OpenFile, "Open the hook script", path), CopyLine() }
            : [CopyLine()];
        return Fail(found, new DoctorText("Create an executable pre-push hook and put the CommitFence line first."),
            DoctorImpact.NoReview, actions);
    }

    private DoctorCheckResult ReadHook(DoctorContext context, string path, IReadOnlyList<string> lines, int first, int review)
    {
        var shownPath = CodeText(ShownPath(context, path));
        if(review < 0)
        {
            return ScriptFailure(path, lines, null, new DoctorText("No line of ", shownPath, " runs ", CodeText("CommitFence"), "."),
                new DoctorText("Add the CommitFence line as the first command of the hook script."), DoctorImpact.NoReview);
        }

        var command = lines[review].Trim();
        var argumentsEnd = ReviewLine().Match(command).Length;
        if(!StopsPush().IsMatch(command.AsSpan(argumentsEnd)))
        {
            return ScriptFailure(path, lines, review + 1,
                new DoctorText($"Line {review + 1} does not end in ", CodeText(_stop), ", so it does not explicitly stop a push when CommitFence fails."),
                new DoctorText("Replace the hook line with the line below so it can stop a push. CommitFence does not change a hook script it did not write."),
                DoctorImpact.HookDoesNotStopPush);
        }

        if(review != first)
            return OutOfOrder(path, shownPath, lines, first, review);

        return Pass(new DoctorText($"Line {review + 1} of ", shownPath, ", ending in ", CodeText(_stop), ", so it can stop a push."));
    }

    private DoctorCheckResult OutOfOrder(string path, DoctorTextPart shownPath, IReadOnlyList<string> lines, int first, int review)
    {
        var prior = lines[first].Trim();
        var readsInput = prior == "git lfs pre-push" || prior == "git lfs pre-push \"$@\"";
        var consequence = readsInput
            ? ", reads the push's ref lines before it, so CommitFence gets none."
            : ", runs before it and may read the push's ref lines.";
        var found = new DoctorText($"It runs on line {review + 1} of ", shownPath, $". Line {first + 1}, ", CodeText(prior), consequence);
        var move = first == 0 ? "Move the CommitFence line above line 1" : "Move the hook line up, under the first line";
        var fix = new DoctorText(move + ", so it reads the ref lines first. CommitFence does not change a hook script it did not write.");
        var impact = readsInput ? DoctorImpact.HookOrder : DoctorImpact.HookOrderUncertain;
        var (now, afterMove) = DoctorExcerpt.HookMove(path, lines, first + 1, review + 1);
        return Fail(found, fix, impact, ScriptActions(path, now.Line), now, afterMove: afterMove);
    }

    private DoctorCheckResult ScriptFailure(string path, IReadOnlyList<string> lines, int? line, DoctorText found, DoctorText fix, DoctorImpact impact)
    {
        return Fail(found, fix, impact, ScriptActions(path, line), DoctorExcerpt.Around(path, lines, line));
    }

    private static List<DoctorAction> ScriptActions(string path, int? line)
    {
        return [new DoctorAction(DoctorActionKind.OpenFile, "Open the hook script", path, line), CopyLine()];
    }

    private static string ShownPath(DoctorContext context, string path)
    {
        return Path.GetRelativePath(context.Repository.WorkFolder, path).Replace('\\', '/');
    }

    private static DoctorAction CopyLine()
    {
        return new DoctorAction(DoctorActionKind.CopyText, "Copy the hook line", HookLine.PrePush);
    }

    private static int FindLine(IReadOnlyList<string> lines, Func<string, bool> matches)
    {
        for(var index = 0; index < lines.Count; index++)
        {
            if(matches(lines[index].Trim()))
                return index;
        }

        return -1;
    }
}
