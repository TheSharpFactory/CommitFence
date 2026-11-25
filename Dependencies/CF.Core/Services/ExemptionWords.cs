using CF.Core.Code;

namespace CF.Core.Services;

/// <summary>The words about one exemption that the screen and the command share.</summary>
public static class ExemptionWords
{
    /// <summary>The title of the state with no exemption.</summary>
    public const string EmptyTitle = "No exemptions";

    /// <summary>The sentence of the state with no exemption.</summary>
    public const string EmptySentence = "No line of CommitFence sets a rule aside. When one has to, name the rule and say why, on the line or the line above:";

    /// <summary>The example the state with no exemption gives.</summary>
    public const string EmptyExample = "// commitfence:allow TEAM010: generated code keeps its braces";

    /// <summary>The title of a scan of a repository with no commit.</summary>
    public const string NoCommitTitle = "Nothing to scan yet";

    /// <summary>What a row says when no reason was written.</summary>
    public const string NoReason = "No reason given";

    /// <summary>The reason as the row shows it: the words written, or "No reason given".</summary>
    public static string ReasonOf(ExemptionEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.Reason) ? NoReason : entry.Reason;
    }

    /// <summary>The form of a pragma or an attribute as it is written; null for the allow comment, which the row does not name.</summary>
    public static string? FormOf(ExemptionForm form)
    {
        return form switch
        {
            ExemptionForm.Pragma => "#pragma warning disable",
            ExemptionForm.SuppressMessage => "[SuppressMessage]",
            ExemptionForm.AllowComment => null,
            _ => throw new ArgumentOutOfRangeException(nameof(form))
        };
    }
}
