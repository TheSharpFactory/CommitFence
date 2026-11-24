using System.Text;

namespace CF.Meaning;

/// <summary>
/// Writes the fields of a fact into one line.
/// </summary>
/// <remarks>
/// A fact is fields separated by a vertical bar; the characters that would break the line or the fields are written as
/// a percent sign and two hexadecimal digits.
/// </remarks>
internal static class FactText
{
    public static string Join(params string[] fields)
    {
        return string.Join("|", fields.Select(Escape));
    }

    private static string Escape(string field)
    {
        var builder = new StringBuilder();
        foreach(var letter in field)
            AppendLetter(builder, letter);

        return builder.ToString();
    }

    private static void AppendLetter(StringBuilder builder, char letter)
    {
        if(letter == '%' || letter == '|' || letter == '\n' || letter == '\r')
            builder.Append('%').Append(((int)letter).ToString("X2"));
        else
            builder.Append(letter);
    }
}