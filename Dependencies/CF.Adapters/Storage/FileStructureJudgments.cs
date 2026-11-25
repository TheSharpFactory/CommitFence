using System.Text.Json;
using CF.Core.Ports;

namespace CF.Adapters.Storage;

/// <summary>Keeps the last judgment of layer directions in a repository's data folder.</summary>
public sealed class FileStructureJudgments(IDataFolder dataFolder, IFileSystem files) : IStructureJudgments
{
    private const string _fileName = "structure-judgment.json";

    /// <inheritdoc />
    public StructureJudgment? Read(RepositoryLocation repository)
    {
        try
        {
            var path = Path.Combine(FolderOf(repository), _fileName);
            if(!files.FileExists(path))
                return null;

            var judgment = JsonSerializer.Deserialize(files.ReadAllText(path), StructureJudgmentJsonContext.Default.StructureJudgment);
            return IsValid(judgment) ? judgment : null;
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Save(RepositoryLocation repository, StructureJudgment judgment)
    {
        string? temporary = null;
        try
        {
            var folder = FolderOf(repository);
            files.CreateFolder(folder);
            var path = Path.Combine(folder, _fileName + "." + Guid.NewGuid().ToString("N") + ".new");
            files.WriteNewFile(path, JsonSerializer.Serialize(judgment, StructureJudgmentJsonContext.Default.StructureJudgment));
            temporary = path;
            files.ReplaceFile(temporary, Path.Combine(folder, _fileName));
            temporary = null;
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException)
        {
            if(temporary != null)
                RemoveTemporary(temporary);
        }
    }

    private static bool IsValid(StructureJudgment? judgment)
    {
        return judgment != null && judgment.EndedAt != default && judgment.Breaches != null
            && judgment.Breaches.All(breach => breach != null && breach.FromModule != null && breach.ToModule != null
                && breach.Member != null && breach.UsedType != null && breach.File != null && breach.Line > 0);
    }

    private void RemoveTemporary(string path)
    {
        try
        {
            files.DeleteFile(path);
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException)
        {
            // A temporary record that cannot be removed does not affect the review.
        }
    }

    private string FolderOf(RepositoryLocation repository)
    {
        return RepositoryDataFolder.For(dataFolder.RootFolder, repository);
    }
}
