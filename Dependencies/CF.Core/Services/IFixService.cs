using CF.Core.Fixes;

namespace CF.Core.Services;

/// <summary>Prepares and applies file-wide fixes on request.</summary>
public interface IFixService
{
    /// <summary>Previews a fix without touching the working folder.</summary>
    FixAnswer SuggestFix(FixRequest request);

    /// <summary>Applies the preview if the working file still matches the reviewed file.</summary>
    FixApplied ApplyFix(Fix fix);
}
