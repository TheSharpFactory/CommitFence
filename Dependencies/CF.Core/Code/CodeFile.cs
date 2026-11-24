namespace CF.Core.Code;

/// <summary>
/// A source file, as text, handed to a language's reader.
/// </summary>
public sealed class CodeFile
{
    /// <summary>
    /// Creates a code file.
    /// </summary>
    public CodeFile(string path, string language, string content)
    {
        Path = path;
        Language = language;
        Content = content;
    }

    /// <summary>
    /// The path of the file, relative to the repository, with forward slashes.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The identifier of the language, such as csharp.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// The text of the file.
    /// </summary>
    public string Content { get; }
}