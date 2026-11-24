using System.Text.Json;
using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Reads a compiler log as a stream through source-generated JSON, so that a large log is never held as text. A log
/// without a run is not evidence.
/// </summary>
internal sealed class SarifReader
{
    private readonly IFileSystem _fileSystem;

    public SarifReader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// The first run of the log. Throws <see cref="JsonException" /> when the log cannot be read or has no run.
    /// </summary>
    public SarifRunDto Read(string path)
    {
        using(var stream = _fileSystem.OpenRead(path))
        {
            var log = JsonSerializer.Deserialize(stream, SarifJsonContext.Default.SarifLogDto);
            var run = log?.Runs?.FirstOrDefault();
            if(run == null)
                throw new JsonException("the log has no run");

            return run;
        }
    }
}