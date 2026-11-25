namespace CF.Core.Ports;

/// <summary>The facts of a use that breaks a layer direction.</summary>
public sealed record StructureBreach(string FromModule, string ToModule, string Member, string UsedType, string File, int Line)
{
    /// <summary>Describes the use in the words shared by the screen and the command.</summary>
    public string Sentence
    {
        get
        {
            var user = Member.Length == 0 ? "Code outside any member" : Member;
            return $"{FromModule} may not use {ToModule}. {user} uses {UsedType}, in {File}, line {Line}.";
        }
    }
}
