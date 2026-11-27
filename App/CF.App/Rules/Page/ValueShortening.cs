using System.Globalization;

namespace CF.App.Rules.Page;

/// <summary>
/// What a value of a token shows when its room is shorter than the whole value: a list keeps its first value and says how many more
/// there are, and a name or a path that is still too long is cut in the middle, keeping its first folder and its last name. The whole
/// value is always one step away, in the token's picker and its tooltip.
/// </summary>
internal static class ValueShortening
{
    private const string _ellipsis = "…";

    private static readonly char[] _separators = ['/', '\\'];

    /// <summary>
    /// The value as it is drawn: the text, and how many more values the list holds that are not drawn (0 when none is left out).
    /// </summary>
    internal readonly record struct Shown(string Text, int More);

    /// <summary>
    /// The whole value as one text: the values of a list read "a, b".
    /// </summary>
    public static string Whole(IReadOnlyList<string> values, bool isList)
    {
        return string.Join(", ", isList ? values : values.Take(1));
    }

    /// <summary>
    /// Shortens the value to fit the room. The measure answers how wide a text is; the width of the "+N" box and its gap is what the
    /// caller answers for a count.
    /// </summary>
    public static Shown Shorten(IReadOnlyList<string> values, bool isList, double room, Func<string, double> measure, Func<int, double> moreWidth)
    {
        var whole = Whole(values, isList);
        if(measure(whole) <= room)
            return new Shown(whole, 0);

        if(isList && values.Count > 1)
            return ShortenList(values, room, measure, moreWidth);

        return new Shown(CutMiddle(whole, room, measure), 0);
    }

    /// <summary>
    /// A list's first value is drawn whole beside "+N" whenever the two fit together. Only a first value that cannot be whole is cut:
    /// a path in the middle, keeping its first folder and its last name, and a name at its end. When the cut value and the badge still do
    /// not fit together, there is room for an ellipsis alone, which says that more is there without claiming a width it does not have.
    /// </summary>
    private static Shown ShortenList(IReadOnlyList<string> values, double room, Func<string, double> measure, Func<int, double> moreWidth)
    {
        var more = values.Count - 1;
        var badge = moreWidth(more);
        var first = values[0];
        if(measure(first) + badge <= room)
            return new Shown(first, more);

        var cut = first.IndexOfAny(_separators) >= 0 ? CutMiddle(first, room - badge, measure) : CutEnd(first, room - badge, measure);
        return measure(cut) + badge <= room ? new Shown(cut, more) : new Shown(_ellipsis, 0);
    }

    /// <summary>
    /// Cuts a text at its end with "…" so that it fits; with no room for even one character, an ellipsis alone.
    /// </summary>
    public static string CutEnd(string text, double room, Func<string, double> measure)
    {
        if(measure(text) <= room)
            return text;

        var elements = StringInfo.ParseCombiningCharacters(text);
        for(var kept = elements.Length - 1; kept > 0; kept--)
        {
            var cut = text[..elements[kept]] + _ellipsis;
            if(measure(cut) <= room)
                return cut;
        }

        return _ellipsis;
    }

    /// <summary>
    /// Cuts a text in the middle with "…" so that it fits. A path keeps its first folder and its last name; a name with no separator
    /// keeps its start and its end; with no room for even that, an ellipsis alone. It is never longer than the text.
    /// </summary>
    public static string CutMiddle(string text, double room, Func<string, double> measure)
    {
        if(measure(text) <= room)
            return text;

        var elements = StringInfo.ParseCombiningCharacters(text);
        var headEnd = FirstFolderEnd(text);
        var tailStart = text.LastIndexOfAny(_separators);
        if(headEnd > 0 && headEnd < tailStart && Array.BinarySearch(elements, headEnd) >= 0 && Array.BinarySearch(elements, tailStart) >= 0)
        {
            var path = text[..headEnd] + _ellipsis + text[tailStart..];
            if(measure(path) <= room)
                return path;
        }

        for(var kept = elements.Length - 1; kept > 0; kept--)
        {
            var cut = Join(text, elements, kept);
            if(measure(cut) <= room)
                return cut;
        }

        return _ellipsis;
    }

    private static string Join(string text, int[] elements, int kept)
    {
        var front = (kept + 1) / 2;
        var back = kept / 2;
        var tailStart = back == 0 ? text.Length : elements[^back];
        return text[..elements[front]] + _ellipsis + text[tailStart..];
    }

    private static int FirstFolderEnd(string text)
    {
        var start = text.Length > 2 && text[1] == ':' ? 3 : 0;
        return text.IndexOfAny(_separators, start) + 1;
    }
}
