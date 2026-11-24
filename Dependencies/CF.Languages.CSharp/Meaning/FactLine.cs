using System.Globalization;
using System.Text;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// Reads the line of one fact of the meaning analyzer.
/// </summary>
/// <remarks>
/// The line is fields separated by a vertical bar, in which a percent sign and two hexadecimal digits stand for a
/// character that would break the line or the fields.
/// </remarks>
internal static class FactLine
{
    public static string[] Split(string line)
    {
        return line.Split('|').Select(Unescape).ToArray();
    }

    public static int ReadNumber(string text, string what)
    {
        if(!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            throw new MeaningLogException($"a fact of the meaning analyzer has '{text}' where {what} should be a number");

        return number;
    }

    private static string Unescape(string field)
    {
        if(!field.Contains('%', StringComparison.Ordinal))
            return field;

        var builder = new StringBuilder();
        var index = 0;
        while(index < field.Length)
            index += AppendNext(builder, field, index);

        return builder.ToString();
    }

    private static int AppendNext(StringBuilder builder, string field, int index)
    {
        if(field[index] == '%' && index + 2 < field.Length && TryReadHex(field, index + 1, out var letter))
        {
            builder.Append(letter);
            return 3;
        }

        builder.Append(field[index]);
        return 1;
    }

    private static bool TryReadHex(string field, int start, out char letter)
    {
        letter = '\0';
        if(!int.TryParse(field.AsSpan(start, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            return false;

        letter = (char)value;
        return true;
    }
}