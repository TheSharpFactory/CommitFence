namespace CF.Core.Rules.Settings;

/// <summary>
/// The settings file is wrong. The message is plain; the line and the column, counted from 1, say where, when the
/// problem has a place.
/// </summary>
public sealed class SettingsException : Exception
{
    /// <summary>
    /// A problem with no place in the file, such as a file that is missing.
    /// </summary>
    public SettingsException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// A problem at a line and a column of the file.
    /// </summary>
    public SettingsException(string message, int? line, int? column)
        : base(message)
    {
        Line = line;
        Column = column;
    }

    /// <summary>
    /// The line, counted from 1, or nothing.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// The column, counted from 1, or nothing.
    /// </summary>
    public int? Column { get; }
}