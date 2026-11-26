using CF.Core.Findings;
using CF.Core.Fixes;
using CF.Core.Review;

namespace CF.App.Review;

/// <summary>Keeps finding state and file-wide previews while the same review is displayed or filtered.</summary>
internal sealed class ReviewFixSession
{
    private readonly Dictionary<(string Tree, string? File, string Rule, string? Project, string? Framework), Task<FixAnswer>> _previews = new();
    private readonly Dictionary<string, HashSet<Finding>> _applied = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public Dictionary<Finding, FindingViewModel> Models { get; } = new Dictionary<Finding, FindingViewModel>();

    public int RecordFix(Fix fix, string? tree, ReviewResult result)
    {
        var covered = result.Findings.Where(finding => _applied.Comparer.Equals(finding.File, fix.File)
            && finding.RuleId == fix.RuleId && tree != null && result.TreeOf(finding) == tree).ToHashSet();
        _applied[fix.File] = covered;
        foreach(var (finding, model) in Models)
            UpdateFinding(finding, model);

        return covered.Count;
    }

    public void UpdateFinding(Finding finding, FindingViewModel model)
    {
        if(finding.File != null && _applied.TryGetValue(finding.File, out var covered))
            model.FileWasFixed(covered.Contains(finding));
    }

    public Task<FixAnswer> PrepareAsync(ReviewFixQueue queue, FixRequest request)
    {
        var finding = request.Finding;
        var key = (request.Tree, finding.File, finding.RuleId, finding.ProjectFile, finding.BuildFramework);
        if(!_previews.TryGetValue(key, out var answer))
        {
            answer = queue.PrepareAsync(request, () => finding.File != null && _applied.ContainsKey(finding.File));
            _previews.Add(key, answer);
        }

        return answer;
    }
}
