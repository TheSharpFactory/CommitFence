using System.Text.Json;
using CF.Core.Ports;

namespace CF.Adapters.Storage;

/// <summary>The append-only history in the local data folder for a repository.</summary>
public sealed class FileReviewHistory(IDataFolder dataFolder) : IReviewHistory
{
    private const string _fileName = "history.jsonl";

    /// <inheritdoc />
    public ReviewHistoryRead Read(RepositoryLocation repository)
    {
        var records = new List<ReviewRecord>();
        var unreadable = 0;
        var opened = false;
        try
        {
            using var file = new FileStream(PathOf(repository), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            opened = true;
            using var reader = new StreamReader(file);
            while(reader.ReadLine() is { } line)
            {
                if(ReadLine(line) is { } record)
                    records.Add(record);
                else
                    unreadable++;
            }
        }
        catch(Exception exception) when(!opened && exception is (FileNotFoundException or DirectoryNotFoundException))
        {
            // A repository can have no history yet.
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException)
        {
            return new ReviewHistoryRead(records, unreadable, exception.Message);
        }

        return new ReviewHistoryRead(records, unreadable);
    }

    /// <inheritdoc />
    public void Append(RepositoryLocation repository, ReviewRecord record)
    {
        var path = PathOf(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        var needsLineEnd = false;
        if(file.Length > 0)
        {
            file.Seek(-1, SeekOrigin.End);
            needsLineEnd = file.ReadByte() != '\n';
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(record, ReviewHistoryJsonContext.Default.ReviewRecord);
        var offset = needsLineEnd ? 1 : 0;
        var line = new byte[json.Length + offset + 1];
        if(needsLineEnd)
            line[0] = (byte)'\n';

        json.CopyTo(line, offset);
        line[^1] = (byte)'\n';
        file.Write(line);
    }

    private static ReviewRecord? ReadLine(string line)
    {
        try
        {
            var record = JsonSerializer.Deserialize(line, ReviewHistoryJsonContext.Default.ReviewRecord);
            return record is { Format: ReviewRecord.CurrentFormat, DurationMilliseconds: >= 0, FilesReviewed: >= 0 } &&
                record.EndedAt != default && record.EndedAt.Offset == TimeSpan.Zero && Enum.IsDefined(record.Source) &&
                Enum.IsDefined(record.Outcome) && record.Findings != null &&
                record.Findings.All(finding => finding != null && !string.IsNullOrWhiteSpace(finding.RuleId) && finding.Count > 0)
                ? record : null;
        }
        catch(JsonException)
        {
            return null;
        }
    }

    private string PathOf(RepositoryLocation repository)
    {
        return Path.Combine(RepositoryDataFolder.For(dataFolder.RootFolder, repository), _fileName);
    }
}
