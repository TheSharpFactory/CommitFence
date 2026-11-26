using CF.Core.Findings;
using CF.Core.Fixes;

namespace CF.Core.Code;

/// <summary>A language's tool for fixing a finding in the private copy.</summary>
public interface IFixer
{
    /// <summary>Runs the tool for the finding, or reports why it did not run or finish.</summary>
    FixAttempt Change(string treeFolder, Finding finding);
}
