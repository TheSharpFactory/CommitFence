using System.Text;
using System.Text.RegularExpressions;

namespace CF.Languages.CSharp.EditorConfig;

/// <summary>
/// The pattern of an .editorconfig section, such as *.cs or src/**.cs or *.{cs,vb}, and whether it matches a path
/// relative to the folder of the file (written with forward slashes). Case counts, as in the compiler.
/// </summary>
internal sealed class SectionPattern
{
    private readonly IReadOnlyList<Regex> _alternatives;

    public SectionPattern(string glob)
    {
        _alternatives = ExpandBraces(glob).Select(ToRegex).ToList();
    }

    public bool Matches(string relativePath)
    {
        return _alternatives.Any(alternative => alternative.IsMatch(relativePath));
    }

    private static List<string> ExpandBraces(string glob)
    {
        var group = FindGroup(glob);
        if(group == null)
            return new List<string> { glob };

        var bounds = new List<int> { group.Open }.Concat(group.Commas).Append(group.Close).ToList();
        var prefix = glob.Substring(0, group.Open);
        var suffix = glob.Substring(group.Close + 1);
        var expanded = new List<string>();
        for(var index = 0; index < bounds.Count - 1; index++)
            expanded.AddRange(ExpandBraces(prefix + glob.Substring(bounds[index] + 1, bounds[index + 1] - bounds[index] - 1) + suffix));

        return expanded;
    }

    private static BraceGroup? FindGroup(string glob)
    {
        for(var open = 0; open < glob.Length; open++)
        {
            if(glob[open] == '\\')
            {
                open++;
                continue;
            }

            var group = glob[open] == '{' ? ReadGroup(glob, open) : null;
            if(group != null)
                return group;
        }

        return null;
    }

    private static BraceGroup? ReadGroup(string glob, int open)
    {
        var commas = new List<int>();
        var depth = 0;
        for(var index = open; index < glob.Length; index++)
        {
            var current = glob[index];
            if(current == '\\')
                index++;
            else if(current == '{')
                depth++;
            else if(current == ',' && depth == 1)
                commas.Add(index);
            else if(current == '}' && --depth == 0)
                return commas.Count > 0 ? new BraceGroup(open, index, commas) : null;
        }

        return null;
    }

    private static Regex ToRegex(string glob)
    {
        var text = glob.TrimStart('/');
        var builder = new StringBuilder(glob.Contains('/') ? "^" : "^(?:.*/)?");
        var index = 0;
        while(index < text.Length)
            index = AppendToken(builder, text, index) + 1;

        return new Regex(builder.Append('$').ToString(), RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Appends the regular expression of the token at the index and returns the index of the token's last character.
    /// </summary>
    private static int AppendToken(StringBuilder builder, string text, int index)
    {
        var current = text[index];
        var next = index + 1 < text.Length ? text[index + 1] : '\0';
        if(current == '\\' && next != '\0')
            builder.Append(Regex.Escape(next.ToString()));
        else if(current == '*' && next == '*')
            builder.Append(".*");
        else if(current == '*')
            builder.Append("[^/]*");
        else if(current == '?')
            builder.Append("[^/]");
        else if(current == '[' && text.IndexOf(']', index + 1) > index + 1)
            return AppendClass(builder, text, index);
        else
            builder.Append(Regex.Escape(current.ToString()));

        return (current == '\\' && next != '\0') || (current == '*' && next == '*') ? index + 1 : index;
    }

    private static int AppendClass(StringBuilder builder, string text, int index)
    {
        var close = text.IndexOf(']', index + 1);
        var content = text.Substring(index + 1, close - index - 1);
        var negated = content.StartsWith('!');
        var members = (negated ? content.Substring(1) : content).Replace("\\", "\\\\", StringComparison.Ordinal);
        builder.Append(negated ? "[^" : "[").Append(members).Append(']');

        return close;
    }

    private sealed record BraceGroup(int Open, int Close, IReadOnlyList<int> Commas);
}
