using System.Globalization;
using CF.Core.Ports;

namespace CF.Core.Fixes;

/// <summary>A file-wide fix with its exact input, output and displayed patch.</summary>
public sealed class Fix
{
    /// <summary>Creates a preview for a single file in the repository.</summary>
    public Fix(RepositoryLocation repository, string file, string ruleId, byte[] before, byte[] after, string patch, string byteChanges = "")
    {
        Repository = repository;
        File = file;
        RuleId = ruleId;
        Before = before.AsSpan().ToArray();
        After = after.AsSpan().ToArray();
        Patch = patch;
        ByteChanges = byteChanges;
    }

    /// <summary>The repository the preview belongs to.</summary>
    public RepositoryLocation Repository { get; }

    /// <summary>The path of the file relative to the repository.</summary>
    public string File { get; }

    /// <summary>The rule this preview fixes.</summary>
    public string RuleId { get; }

    /// <summary>The reviewed bytes.</summary>
    public ReadOnlyMemory<byte> Before { get; }

    /// <summary>The complete fixed file.</summary>
    public ReadOnlyMemory<byte> After { get; }

    /// <summary>The complete unified diff as git printed it.</summary>
    public string Patch { get; }

    /// <summary>The line ending or encoding changes that the text patch cannot show.</summary>
    public string ByteChanges { get; }

    /// <summary>Explains the size and reach of the fix in a push or folder review.</summary>
    public string DescribeReach(bool folderReview)
    {
        var (places, added, removed) = CountPatch();
        var size = string.Create(CultureInfo.InvariantCulture, $"{places} {(places == 1 ? "place" : "places")} (+{added} −{removed})");
        var reach = RuleId == "IDE0055"
            ? $"The fix formats the whitespace of this whole file: {size}"
            : $"The fix changes {size}, every {RuleId} in this file";
        return reach + (folderReview ? "." : ", also lines this push did not change.");
    }

    // a place is a run of changed lines: git joins changes a few lines apart into one hunk, but a reader sees two
    private (int Places, int Added, int Removed) CountPatch()
    {
        var places = 0;
        var added = 0;
        var removed = 0;
        var inHunk = false;
        var inRun = false;
        foreach(var line in Patch.Split('\n'))
        {
            if(line.StartsWith('\\'))
                continue;

            if(line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                inHunk = true;
                inRun = false;
                continue;
            }

            var isChange = inHunk && (line.StartsWith('+') || line.StartsWith('-'));
            if(isChange && !inRun)
                places++;

            if(isChange && line.StartsWith('+'))
                added++;
            else if(isChange)
                removed++;

            inRun = isChange;
        }

        return (places, added, removed);
    }
}
