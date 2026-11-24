using System.Globalization;

namespace CF.Meaning;

/// <summary>
/// The places in a file where one use is found, each as a line and a column counted from 1, in the order of the file.
/// </summary>
internal sealed class UsePlaces
{
    private readonly SortedSet<(int Line, int Column)> _places = new SortedSet<(int Line, int Column)>();

    public void Add(int line, int column)
    {
        _places.Add((line, column));
    }

    public string ToText()
    {
        return string.Join(",", _places.Select(place => place.Line.ToString(CultureInfo.InvariantCulture) + ":" + place.Column.ToString(CultureInfo.InvariantCulture)));
    }
}