namespace CF.Core.Code;

/// <summary>
/// Reads one source file into its artifacts, each with the properties its catalog promises.
/// </summary>
public interface ICodeReader
{
    /// <summary>
    /// The artifacts of the file, in the order they start in the file.
    /// </summary>
    /// <exception cref="CodeReadException">The file does not parse, so its artifacts cannot be trusted.</exception>
    IReadOnlyList<Artifact> Read(CodeFile file);

    /// <summary>
    /// The artifacts of a piece of code that a person typed or pasted: a whole file as <see cref="Read(CodeFile)" /> reads it,
    /// or else a part of one, such as members of a type or the statements of a method. Lines and columns are those of the
    /// text given.
    /// </summary>
    /// <exception cref="CodeReadException">The text is neither a whole file nor a part of one that parses.</exception>
    IReadOnlyList<Artifact> ReadPiece(CodeFile file);

    /// <summary>
    /// The artifacts of the file as the compiler reads it when these symbols are defined: code switched by a condition
    /// on a symbol is read, or left out, accordingly.
    /// </summary>
    /// <exception cref="CodeReadException">The file does not parse with these symbols, so its artifacts cannot be
    /// trusted.</exception>
    IReadOnlyList<Artifact> Read(CodeFile file, IReadOnlyList<string> definedSymbols);

    /// <summary>
    /// Whether the file holds code switched by a condition on a symbol, of which a reading with no symbols leaves some
    /// out. A file that does not parse has none to speak of.
    /// </summary>
    bool HasConditionalSections(CodeFile file);
}