using System.Globalization;

namespace CF.Core.Setup;

/// <summary>
/// A version of the product as it is compared: four whole numbers, never text. A version written
/// with fewer parts has zeros for the rest, so 0.1 is 0.1.0.0.
/// </summary>
public sealed class ProductVersion
{
    private const int _partCount = 4;
    private const int _longestPart = 6;

    private readonly int[] _parts;

    private ProductVersion(int[] parts)
    {
        _parts = parts;
    }

    /// <summary>
    /// Reads one to four numbers separated by dots, such as 0.1 or 1.0.0.12, and nothing else: no label, no spaces.
    /// </summary>
    /// <remarks>
    /// This is how a repository's value is read. Returns nothing when the text is not a version.
    /// </remarks>
    public static ProductVersion? TryParse(string text)
    {
        return ReadNumbers(text);
    }

    /// <summary>
    /// Reads the program's own version as the build wrote it: the same numbers, and anything after a plus or a hyphen
    /// (a label such as -rc1 or a build number) is not part of the version and is dropped.
    /// </summary>
    /// <remarks>
    /// Returns nothing when the text is not a version.
    /// </remarks>
    public static ProductVersion? TryParseOwn(string text)
    {
        return ReadNumbers(DropLabel(text.Trim()));
    }

    private static ProductVersion? ReadNumbers(string text)
    {
        var numbers = text.Split('.');
        if(numbers.Length > _partCount)
            return null;

        var parts = new int[_partCount];
        for(var index = 0; index < numbers.Length; index++)
        {
            if(!IsWholeNumber(numbers[index]))
                return null;

            parts[index] = int.Parse(numbers[index], CultureInfo.InvariantCulture);
        }

        return new ProductVersion(parts);
    }

    /// <summary>
    /// Compares part by part, from the first.
    /// </summary>
    public int CompareTo(ProductVersion other)
    {
        for(var index = 0; index < _partCount; index++)
        {
            var difference = _parts[index].CompareTo(other._parts[index]);
            if(difference != 0)
                return difference;
        }

        return 0;
    }

    /// <summary>
    /// The four numbers separated by dots.
    /// </summary>
    public override string ToString()
    {
        return string.Join(".", _parts);
    }

    private static string DropLabel(string text)
    {
        var end = text.IndexOfAny(new[] { '+', '-' });
        return end < 0 ? text : text.Substring(0, end);
    }

    private static bool IsWholeNumber(string text)
    {
        return text.Length > 0 && text.Length <= _longestPart && text.All(char.IsAsciiDigit);
    }
}