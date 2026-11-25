namespace CF.Core.Setup;

/// <summary>A run of prose or code in a Doctor sentence.</summary>
/// <param name="Text">The text to display.</param>
/// <param name="IsCode">Whether this is a path, command or name to show in code type.</param>
public sealed record DoctorTextPart(string Text, bool IsCode)
{
    /// <summary>Creates a run of prose.</summary>
    public static implicit operator DoctorTextPart(string text)
    {
        return new DoctorTextPart(text, false);
    }

    /// <summary>Creates a run of prose.</summary>
    public static DoctorTextPart FromString(string text)
    {
        return text;
    }

    /// <summary>Creates a run of code.</summary>
    public static DoctorTextPart CodeText(string text)
    {
        return new DoctorTextPart(text, true);
    }
}
