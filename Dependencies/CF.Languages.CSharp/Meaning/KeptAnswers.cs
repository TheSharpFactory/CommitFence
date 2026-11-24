using System.Text.Json;
using CF.Core.Ports;
using CF.Languages.CSharp.Toolchain;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// The answers kept for trees already built, in the product's data folder, one file for each key (see
/// <see cref="MeaningKey" />).
/// </summary>
/// <remarks>
/// The tree before a push has normally been reviewed already as the tree after an earlier push, so one build is enough.
/// A kept file is read like a log: its facts must match their closing entry, or they are not used.
/// </remarks>
internal sealed class KeptAnswers
{
    private readonly IFileSystem _fileSystem;
    private readonly string _folder;

    public KeptAnswers(IFileSystem fileSystem, IDataFolder dataFolder)
    {
        _fileSystem = fileSystem;
        _folder = ToolchainPaths.Combine(Path.Combine(dataFolder.RootFolder, "csharp"), "answers");
    }

    /// <summary>
    /// The kept facts of every part asked for, or nothing when the key has no file, the file cannot be trusted, or some
    /// part is not in it.
    /// </summary>
    public IReadOnlyList<UnitFacts>? Find(string key, IReadOnlyList<string> parts)
    {
        var file = ReadFile(key);
        if(file?.Parts == null || file.TreeFolder == null)
            return null;

        var reader = new MeaningFactReader(file.TreeFolder);
        var found = new List<UnitFacts>();
        foreach(var part in parts)
        {
            var kept = file.Parts.FirstOrDefault(candidate => candidate.Part == part);
            var facts = kept == null ? null : TryRead(reader, kept);
            if(facts == null)
                return null;

            found.Add(facts);
        }

        return found;
    }

    /// <summary>
    /// Keeps the facts under the key, next to the facts of other parts already kept under it.
    /// </summary>
    public void Save(string key, string treeFolder, IReadOnlyList<UnitFacts> units)
    {
        var existing = ReadFile(key);
        var parts = existing?.TreeFolder == treeFolder && existing.Parts != null ? existing.Parts : new List<KeptPartDto>();
        parts.RemoveAll(part => units.Any(unit => unit.PartKey == part.Part));
        parts.AddRange(units.Select(ToDto));

        _fileSystem.CreateFolder(_folder);
        _fileSystem.WriteAllText(PathOf(key), JsonSerializer.Serialize(new KeptFileDto { TreeFolder = treeFolder, Parts = parts }, KeptJsonContext.Default.KeptFileDto));
    }

    private static KeptPartDto ToDto(UnitFacts unit)
    {
        return new KeptPartDto { Part = unit.PartKey, Facts = unit.FactLines.ToList(), Closing = unit.ClosingLine };
    }

    private static UnitFacts? TryRead(MeaningFactReader reader, KeptPartDto kept)
    {
        if(kept.Part == null || kept.Facts == null || kept.Closing == null)
            return null;

        try
        {
            return reader.Read(kept.Facts, new List<string> { kept.Closing }, kept.Part, 0);
        }
        catch(MeaningLogException)
        {
            return null;
        }
    }

    private KeptFileDto? ReadFile(string key)
    {
        var path = PathOf(key);
        if(!_fileSystem.FileExists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize(_fileSystem.ReadAllText(path), KeptJsonContext.Default.KeptFileDto);
        }
        catch(Exception exception) when(exception is JsonException || exception is IOException)
        {
            return null;
        }
    }

    private string PathOf(string key)
    {
        return ToolchainPaths.Combine(_folder, key + ".json");
    }
}