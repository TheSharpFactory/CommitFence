using System.Security.Cryptography;
using System.Text;
using CF.Core.Code;
using CF.Languages.CSharp.Toolchain;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// Turns the notes of the meaning analyzer in one compiler log into facts, after checking them against the closing
/// entry.
/// </summary>
/// <remarks>
/// The check is that there is exactly one closing entry, in the format this version reads, and its count and checksum
/// are those of the facts found. A log without it, or cut short, is not evidence.
/// </remarks>
internal sealed class MeaningFactReader
{
    private const string _formatVersion = "1";
    private const string _closingLabel = "END";
    private const int _closingFields = 6;
    private const int _declarationFields = 8;
    private const int _useFields = 10;

    private readonly string _treeFolder;

    public MeaningFactReader(string treeFolder)
    {
        _treeFolder = treeFolder;
    }

    /// <summary>
    /// The facts of one project and framework.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="MeaningLogException" /> when the log is not complete evidence;
    /// <paramref name="expectedFiles" /> is how many source files the build said the project compiles, and fewer files
    /// analyzed than that is a log that was cut short.
    /// </remarks>
    public UnitFacts Read(IReadOnlyList<string> factLines, IReadOnlyList<string> closingLines, string partKey, int expectedFiles)
    {
        var closing = ReadClosing(closingLines);
        CheckClosing(closing, factLines, expectedFiles);

        var assembly = closing[2];
        var declarations = new List<MeaningDeclaration>();
        var uses = new List<MeaningUse>();
        var foreign = 0;
        foreach(var line in factLines)
        {
            if(!ReadFact(line, assembly, declarations, uses))
                foreign++;
        }

        // a log none of whose facts names a file of the tree was written about another place, and cannot be relied on
        if(foreign > 0 && foreign == factLines.Count)
            throw new MeaningLogException($"the meaning analyzer named no file inside the private copy ({_treeFolder}), so what it says cannot be placed in the repository");

        return new UnitFacts(partKey, factLines, closingLines[0], declarations, uses);
    }

    private static string[] ReadClosing(IReadOnlyList<string> closingLines)
    {
        if(closingLines.Count == 0)
            throw new MeaningLogException("the log has no closing entry of the meaning analyzer, so its facts may be missing or cut short (the repository may have switched analyzers off)");

        if(closingLines.Count > 1)
            throw new MeaningLogException("the log has more than one closing entry of the meaning analyzer");

        var fields = FactLine.Split(closingLines[0]);
        if(fields.Length != _closingFields || fields[0] != _closingLabel)
            throw new MeaningLogException("the closing entry of the meaning analyzer is not written as expected");

        if(fields[1] != _formatVersion)
            throw new MeaningLogException($"the meaning analyzer wrote format {fields[1]}, and this version of CommitFence reads format {_formatVersion}");

        return fields;
    }

    private static void CheckClosing(string[] closing, IReadOnlyList<string> factLines, int expectedFiles)
    {
        var count = FactLine.ReadNumber(closing[4], "the number of facts");
        if(count != factLines.Count)
            throw new MeaningLogException($"the closing entry of the meaning analyzer counts {count} facts and the log holds {factLines.Count}");

        if(closing[5] != Checksum(factLines))
            throw new MeaningLogException("the checksum in the closing entry of the meaning analyzer is not that of the facts in the log");

        var analyzed = FactLine.ReadNumber(closing[3], "the number of files analyzed");
        if(analyzed < expectedFiles)
            throw new MeaningLogException($"the meaning analyzer looked at {analyzed} of the {expectedFiles} source files the project compiles");
    }

    private static string Checksum(IReadOnlyList<string> factLines)
    {
        var sorted = factLines.OrderBy(line => line, StringComparer.Ordinal);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", sorted)));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Takes one fact in, and says whether it was about a file of the tree. A fact about a file from outside it, such as
    /// a source file that a package adds to the compile, is not the repository's and is left out.
    /// </summary>
    private bool ReadFact(string line, string assembly, List<MeaningDeclaration> declarations, List<MeaningUse> uses)
    {
        var fields = FactLine.Split(line);
        var isDeclaration = fields[0] == "D" && fields.Length == _declarationFields;
        if(!isDeclaration && !(fields[0] == "U" && fields.Length == _useFields))
            throw new MeaningLogException("a fact of the meaning analyzer is not written as expected");

        var file = RelativeFile(fields[1]);
        if(file == null)
            return false;

        if(isDeclaration)
            declarations.Add(ReadDeclaration(fields, file, assembly));
        else
            uses.Add(ReadUse(fields, file, assembly));

        return true;
    }

    private static MeaningDeclaration ReadDeclaration(string[] fields, string file, string assembly)
    {
        var start = new SourcePoint(FactLine.ReadNumber(fields[2], "a line"), FactLine.ReadNumber(fields[3], "a column"));
        return new MeaningDeclaration(file, start, fields[4], fields[5], assembly, fields[6], fields[7]);
    }

    private MeaningUse ReadUse(string[] fields, string file, string assembly)
    {
        var places = fields[9].Split(',').Select(ReadPlace).ToList();

        // what is used may be declared in a file from outside the tree: then it has no file of the repository
        return new MeaningUse(file, fields[2], fields[3], assembly, fields[4], fields[5], fields[6], fields[7], RelativeFile(fields[8]) ?? string.Empty, places);
    }

    private static SourcePoint ReadPlace(string text)
    {
        var parts = text.Split(':');
        if(parts.Length != 2)
            throw new MeaningLogException($"a place of a fact of the meaning analyzer is written '{text}' and not as line:column");

        return new SourcePoint(FactLine.ReadNumber(parts[0], "a line"), FactLine.ReadNumber(parts[1], "a column"));
    }

    /// <summary>
    /// The file's path from the top of the tree; nothing for a file that is not inside the tree.
    /// </summary>
    private string? RelativeFile(string path)
    {
        if(path.Length == 0)
            return string.Empty;

        var relative = ToolchainPaths.RelativeTo(_treeFolder, path);

        return Path.IsPathRooted(relative) ? null : relative;
    }
}