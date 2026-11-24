namespace CF.Core.Changes;

/// <summary>
/// Reads the lines git gives a pre-push hook: local ref, local object, remote ref and remote object, separated by
/// blanks.
/// </summary>
public static class PushedRefParser
{
    private const int _fieldCount = 4;
    private const int _shortestObjectId = 40;

    /// <summary>
    /// Reads the whole standard input of the hook. Empty input gives no refs; a line that is cut off, has the wrong
    /// number of fields or holds something that is not an object id makes the input broken.
    /// </summary>
    public static PushedRefs Parse(string text)
    {
        if(text.Length == 0)
            return PushedRefs.None;

        if(!text.EndsWith('\n'))
            return PushedRefs.Broken("the last ref line git gave the hook is cut off");

        var refs = new List<PushedRef>();
        foreach(var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var cleanLine = line.TrimEnd('\r');
            var pushedRef = ParseLine(cleanLine);
            if(pushedRef == null)
                return PushedRefs.Broken($"git gave the hook a ref line that cannot be read: '{cleanLine}'");

            refs.Add(pushedRef);
        }

        return PushedRefs.FromRefs(refs);
    }

    private static PushedRef? ParseLine(string line)
    {
        var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if(fields.Length != _fieldCount)
            return null;

        if(!IsObjectId(fields[1]) || !IsObjectId(fields[3]))
            return null;

        return new PushedRef(fields[0], fields[1], fields[2], fields[3]);
    }

    private static bool IsObjectId(string text)
    {
        return text.Length >= _shortestObjectId && text.All(Uri.IsHexDigit);
    }
}