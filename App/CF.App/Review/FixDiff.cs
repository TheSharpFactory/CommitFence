using System.Globalization;
using CF.App.Rules.Page.Code;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Fixes;

namespace CF.App.Review;

/// <summary>Lines of a complete git patch, with syntax colour and the changed characters marked.</summary>
internal sealed class FixDiff
{
    public static IReadOnlyList<FixDiffLine> Read(Fix fix, Finding finding, IEnumerable<Finding> fileFindings, bool isFolderReview)
    {
        var before = new Source(fix.Before);
        var after = new Source(fix.After);
        var rows = new List<FixDiffLine>();
        var removed = new List<FixDiffLine>();
        var added = new List<FixDiffLine>();
        var oldLine = 0;
        var newLine = 0;
        var inHunk = false;
        foreach(var line in fix.Patch.Split('\n'))
        {
            if(line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                FinishPlace();
                var ranges = line.Split(' ', 4);
                oldLine = FirstLine(ranges[1]);
                newLine = FirstLine(ranges[2]);
                inHunk = true;
            }
            else if(inHunk && line.Length > 0)
            {
                switch(line[0])
                {
                    case ' ':
                        FinishPlace();
                        rows.Add(after.Row(oldLine++, newLine++));
                        break;
                    case '-':
                        var deletion = before.Row(oldLine++, null);
                        removed.Add(deletion);
                        rows.Add(deletion);
                        break;
                    case '+':
                        var insertion = after.Row(null, newLine++);
                        added.Add(insertion);
                        rows.Add(insertion);
                        break;
                }
            }
        }

        FinishPlace();
        return rows;

        void FinishPlace()
        {
            if(removed.Count > 0)
            {
                var firstChanged = rows[rows.Count - removed.Count - added.Count];
                firstChanged.PlaceLabel = PlaceLabel(finding, fileFindings, isFolderReview, removed[0].OldLine!.Value, removed[^1].OldLine!.Value);
            }

            MarkChanges(removed, added);
        }
    }

    private static string PlaceLabel(Finding finding, IEnumerable<Finding> fileFindings, bool isFolderReview, int first, int last)
    {
        if(ContainsLine(finding))
            return "This finding";

        var other = fileFindings.FirstOrDefault(ContainsLine);
        if(other == null)
            return string.Empty;

        var effect = FindingViewModel.ReviewEffectWord(other.Severity, isFolderReview);
        return string.Create(CultureInfo.InvariantCulture, $"Also line {other.Line}, {effect}");

        bool ContainsLine(Finding candidate)
        {
            return candidate.Line is int line && line >= first && line <= last;
        }
    }

    private static int FirstLine(string range)
    {
        var comma = range.IndexOf(',', StringComparison.Ordinal);
        var end = comma < 0 ? range.Length : comma;
        return int.Parse(range.AsSpan(1, end - 1), CultureInfo.InvariantCulture);
    }

    private static void MarkChanges(List<FixDiffLine> removed, List<FixDiffLine> added)
    {
        for(var index = 0; index < Math.Max(removed.Count, added.Count); index++)
        {
            var old = index < removed.Count ? removed[index] : null;
            var updated = index < added.Count ? added[index] : null;
            if(old == null || updated == null)
            {
                MarkLine(old ?? updated!);
                continue;
            }

            var before = old.Text;
            var after = updated.Text;
            var prefix = before.AsSpan().CommonPrefixLength(after);
            var suffix = CommonSuffix(before.AsSpan(prefix), after.AsSpan(prefix));
            old.Mark(prefix, before.Length - prefix - suffix);
            updated.Mark(prefix, after.Length - prefix - suffix);
        }

        removed.Clear();
        added.Clear();
    }

    private static void MarkLine(FixDiffLine line)
    {
        var text = line.Text.AsSpan();
        var content = text.TrimStart();
        if(content.IsEmpty)
            line.Mark(0, text.Length);
        else
            line.Mark(text.Length - content.Length, content.Length);
    }

    private static int CommonSuffix(ReadOnlySpan<char> before, ReadOnlySpan<char> after)
    {
        var count = 0;
        while(count < before.Length && count < after.Length && before[^(count + 1)] == after[^(count + 1)])
            count++;

        return count;
    }

    private sealed class Source
    {
        private readonly string _text;
        private readonly IReadOnlyList<int> _starts;
        private readonly IReadOnlyList<CodeToken> _tokens;

        public Source(ReadOnlyMemory<byte> bytes)
        {
            using var input = new MemoryStream(bytes.ToArray());
            using var reader = new StreamReader(input);
            _text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            _starts = Excerpt.LineStarts(_text);
            _tokens = CSharpTokens.Scan(_text);
        }

        public FixDiffLine Row(int? oldLine, int? newLine)
        {
            var excerpt = Excerpt.Around(_text, _starts, newLine ?? oldLine!.Value, 0);
            var segments = excerpt.Count > 0 ? CodeAround.Segments(_text, excerpt[0].Start, excerpt[0].End, _tokens) : new List<CodeSegment>();
            return new FixDiffLine(oldLine, newLine, segments);
        }
    }
}
