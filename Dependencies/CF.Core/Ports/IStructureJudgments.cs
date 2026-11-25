namespace CF.Core.Ports;

/// <summary>The last review that finished judging layer directions in each repository.</summary>
public interface IStructureJudgments
{
    /// <summary>Reads the last judgment, if it can be read.</summary>
    StructureJudgment? Read(RepositoryLocation repository);

    /// <summary>Replaces the last judgment if it can be saved. Storage failures do not fail a review.</summary>
    void Save(RepositoryLocation repository, StructureJudgment judgment);
}
