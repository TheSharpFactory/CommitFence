using System.Text;

namespace CF.Tests.Review.Support;

/// <summary>
/// Writes the diff git writes with no context lines, for two sets of files, so that a test states the trees and not the
/// hunks.
/// </summary>
internal static class UnifiedDiffMaker
{
    public static string Between(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
    {
        var diff = new StringBuilder();
        foreach(var path in before.Keys.Union(after.Keys).Order(StringComparer.Ordinal))
        {
            var hasBefore = before.TryGetValue(path, out var oldText);
            var hasAfter = after.TryGetValue(path, out var newText);
            if(hasBefore && hasAfter && oldText == newText)
                continue;

            AppendFile(diff, path, hasBefore ? SplitLines(oldText!) : null, hasAfter ? SplitLines(newText!) : null);
        }

        return diff.ToString();
    }

    private static void AppendFile(StringBuilder diff, string path, List<string>? oldLines, List<string>? newLines)
    {
        diff.Append($"diff --git a/{path} b/{path}\n");
        if(oldLines == null)
            diff.Append("new file mode 100644\n--- /dev/null\n").Append($"+++ b/{path}\n");
        else if(newLines == null)
            diff.Append("deleted file mode 100644\n").Append($"--- a/{path}\n+++ /dev/null\n");
        else
            diff.Append($"--- a/{path}\n+++ b/{path}\n");

        foreach(var hunk in FindHunks(oldLines ?? new List<string>(), newLines ?? new List<string>()))
            AppendHunk(diff, hunk);
    }

    private static void AppendHunk(StringBuilder diff, Hunk hunk)
    {
        var oldStart = hunk.Removed.Count == 0 ? hunk.OldBefore : hunk.OldBefore + 1;
        var newStart = hunk.Added.Count == 0 ? hunk.NewBefore : hunk.NewBefore + 1;
        diff.Append($"@@ -{Range(oldStart, hunk.Removed.Count)} +{Range(newStart, hunk.Added.Count)} @@\n");
        foreach(var line in hunk.Removed)
            diff.Append('-').Append(line).Append('\n');

        foreach(var line in hunk.Added)
            diff.Append('+').Append(line).Append('\n');
    }

    private static string Range(int start, int count)
    {
        return count == 1 ? start.ToString() : $"{start},{count}";
    }

    private static List<string> SplitLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        if(lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }

    /// <summary>
    /// The runs of removed and added lines between the two texts, found from their longest common sequence of lines.
    /// </summary>
    private static List<Hunk> FindHunks(List<string> oldLines, List<string> newLines)
    {
        var common = new int[oldLines.Count + 1, newLines.Count + 1];
        for(var i = oldLines.Count - 1; i >= 0; i--)
        {
            for(var j = newLines.Count - 1; j >= 0; j--)
                common[i, j] = oldLines[i] == newLines[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);
        }

        var hunks = new List<Hunk>();
        Hunk? open = null;
        int oldIndex = 0, newIndex = 0;
        while(oldIndex < oldLines.Count || newIndex < newLines.Count)
        {
            if(oldIndex < oldLines.Count && newIndex < newLines.Count && oldLines[oldIndex] == newLines[newIndex])
            {
                open = null;
                oldIndex++;
                newIndex++;
                continue;
            }

            if(open == null)
            {
                open = new Hunk(oldIndex, newIndex);
                hunks.Add(open);
            }

            if(newIndex >= newLines.Count || (oldIndex < oldLines.Count && common[oldIndex + 1, newIndex] >= common[oldIndex, newIndex + 1]))
                open.Removed.Add(oldLines[oldIndex++]);
            else
                open.Added.Add(newLines[newIndex++]);
        }

        return hunks;
    }

    private sealed class Hunk
    {
        public Hunk(int oldBefore, int newBefore)
        {
            OldBefore = oldBefore;
            NewBefore = newBefore;
        }

        public int OldBefore { get; }

        public int NewBefore { get; }

        public List<string> Removed { get; } = new List<string>();

        public List<string> Added { get; } = new List<string>();
    }
}
