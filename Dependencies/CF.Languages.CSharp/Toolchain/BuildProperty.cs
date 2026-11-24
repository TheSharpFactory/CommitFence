namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Writes one property for the build's command line.
/// </summary>
/// <remarks>
/// The build reads a semicolon or a comma in a value as the start of another property and expands a dollar or an at
/// sign, so a value taken from outside (a path, a rule's identifier) is escaped the build's own way and arrives as the
/// text it is.
/// </remarks>
internal static class BuildProperty
{
    public static string Argument(string name, string value)
    {
        return "-p:" + name + "=" + Escape(value);
    }

    /// <summary>
    /// Several values as one property, separated the way the build separates a list.
    /// </summary>
    public static string ListArgument(string name, IEnumerable<string> values)
    {
        return "-p:" + name + "=" + string.Join("%3B", values.Select(Escape));
    }

    private static string Escape(string value)
    {
        return value.Replace("%", "%25", StringComparison.Ordinal).Replace(";", "%3B", StringComparison.Ordinal).Replace(",", "%2C", StringComparison.Ordinal).Replace("$", "%24", StringComparison.Ordinal).Replace("@", "%40", StringComparison.Ordinal);
    }
}