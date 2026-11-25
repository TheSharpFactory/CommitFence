namespace CF.Core.Setup;

/// <summary>A sentence with the runs a face can style individually.</summary>
/// <param name="Parts">The runs in reading order.</param>
public sealed record DoctorText(params IReadOnlyList<DoctorTextPart> Parts)
{
    /// <summary>The sentence as plain text.</summary>
    public override string ToString()
    {
        return string.Concat(Parts.Select(part => part.Text));
    }
}
