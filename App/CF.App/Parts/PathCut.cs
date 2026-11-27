namespace CF.App.Parts;

/// <summary>
/// What of a file's path a line shows: the folders before the name (grey) and the name itself (bold).
/// </summary>
internal readonly record struct PathParts(string Folders, string Name);

/// <summary>
/// Cuts a path to a width at folder boundaries, from the left, and keeps the file name whole: "src/Billing/Invoices/Ledger.cs"
/// becomes "…/Invoices/Ledger.cs", then "…/Ledger.cs". Only when the name alone cannot fit is it cut, at its end.
/// </summary>
internal static class PathCut
{
    private const string _elision = "…/";
    private const string _ellipsis = "…";

    /// <param name="path">The whole path, with "/" between its parts.</param>
    /// <param name="width">The width the line has.</param>
    /// <param name="measure">The width of a text, in bold when the second argument is true.</param>
    public static PathParts Fit(string path, double width, Func<string, bool, double> measure)
    {
        var cut = path.LastIndexOf('/') + 1;
        var folders = path[..cut];
        var name = path[cut..];
        var nameWidth = measure(name, true);
        if(measure(folders, false) + nameWidth <= width)
            return new PathParts(folders, name);

        var slash = -1;
        while((slash = folders.IndexOf('/', slash + 1)) >= 0)
        {
            var kept = _elision + folders[(slash + 1)..];
            if(measure(kept, false) + nameWidth <= width)
                return new PathParts(kept, name);
        }

        return new PathParts(string.Empty, CutEnd(name, width, measure));
    }

    private static string CutEnd(string name, double width, Func<string, bool, double> measure)
    {
        if(measure(name, true) <= width)
            return name;

        var length = name.Length;
        while(length > 0 && measure(name[..length] + _ellipsis, true) > width)
            length--;

        return name[..length] + _ellipsis;
    }
}
