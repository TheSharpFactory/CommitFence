using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;

namespace CF.Core.Review;

/// <summary>Applies well-formed allow comments to findings that reach this review.</summary>
internal sealed class ExemptionFilter
{
    private const string _missingReasonRule = "ALLOW001";
    private readonly Dictionary<string, IReadOnlyList<SourceExemption>> _byPath = new Dictionary<string, IReadOnlyList<SourceExemption>>(StringComparer.Ordinal);

    public IEnumerable<Finding> Remember(ILanguage language, CodeFile file, IReadOnlyList<LineRange>? changedLines)
    {
        var comments = Read(language, file);
        _byPath[file.Path] = comments;
        return comments.Where(comment => comment.Reason == null && (changedLines == null || changedLines.Any(range => range.Touches(comment.Line, comment.Line))))
            .Select(comment => new Finding(_missingReasonRule, FindingSource.Exemption, Severity.Block, language.Id,
                null, null, file.Path, comment.Line, comment.Column, "An exemption needs a reason", null, null));
    }

    public ExemptedFinding? Match(Finding finding, Func<Finding, IReadOnlyList<SourceExemption>>? readSource)
    {
        var path = finding.File;
        if(finding.IsCompileError || finding.Source == FindingSource.Exemption || path == null || finding.Line is not { } line)
            return null;

        if(!_byPath.TryGetValue(path, out var comments))
        {
            comments = readSource?.Invoke(finding) ?? [];
            _byPath.Add(path, comments);
        }

        var comment = comments.LastOrDefault(comment => comment.Reason != null && comment.RuleId == finding.RuleId
            && (comment.Line == line || (comment.CommentOnlyLine && comment.Line + 1 == line)));
        return comment == null ? null : new ExemptedFinding(finding, comment.Line, comment.Reason!);
    }

    public void Clear()
    {
        _byPath.Clear();
    }

    public static IReadOnlyList<SourceExemption> Read(ILanguage language, CodeFile file)
    {
        return file.Content.Contains(AllowComment.Marker, StringComparison.Ordinal)
            ? language.ReadExemptions(file).Where(exemption => exemption.Form == ExemptionForm.AllowComment).ToList()
            : [];
    }
}