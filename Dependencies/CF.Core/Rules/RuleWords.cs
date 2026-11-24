using System.Text;

namespace CF.Core.Rules;

/// <summary>
/// The words a person reads for a name the rules use: statementsInBody is "statements in body", if-statement is "if statement".
/// </summary>
public static class RuleWords
{
    /// <summary>
    /// The name as words: a capital letter or a hyphen starts a new lower-case word.
    /// </summary>
    public static string Of(string name)
    {
        var words = new StringBuilder();
        foreach(var letter in name)
        {
            if(letter == '-')
                words.Append(' ');
            else if(char.IsUpper(letter))
                words.Append(' ').Append(char.ToLowerInvariant(letter));
            else
                words.Append(letter);
        }

        return words.ToString();
    }
}