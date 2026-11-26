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

        var kept = text.Length - 1;
        while(kept > 0 && measure(text[..kept] + _ellipsis) > room)
            kept--;

        return kept == 0 ? _ellipsis : text[..kept] + _ellipsis;
    }

    /// <summary>
    /// Cuts a text in the middle with "…" so that it fits. A path keeps its first folder and its last name; a name with no separator
    /// keeps its start and its end; with no room for even that, an ellipsis alone. It is never longer than the text.
    /// </summary>
    public static string CutMiddle(string text, double room, Func<string, double> measure)
    {
        if(measure(text) <= room)
            return text;

        var head = FirstFolder(text);
        var tail = LastName(text);
        if(head.Length > 0 && tail.Length > 0 && head.Length + tail.Length < text.Length && measure(head + _ellipsis + tail) <= room)
            return head + _ellipsis + tail;

        var kept = text.Length - 1;
        while(kept > 0 && measure(Join(text, kept)) > room)
            kept--;

        return kept == 0 ? _ellipsis : Join(text, kept);
    }

    private static string Join(string text, int kept)
    {
        var front = (kept + 1) / 2;
        var back = kept / 2;
        return text[..front] + _ellipsis + text[^back..];
    }

    private static string FirstFolder(string text)
    {
        var start = text.Length > 2 && text[1] == ':' ? 3 : 0;
        var end = text.IndexOfAny(_separators, start);
        return end < 0 || end >= text.Length - 1 ? string.Empty : text[..(end + 1)];
    }

    private static string LastName(string text)
    {
        var cut = text.LastIndexOfAny(_separators);
        return cut <= 0 ? string.Empty : text[cut..];
    }
}
