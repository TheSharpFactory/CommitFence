using CF.Core.Setup;

namespace CF.App.Parts;

/// <summary>Sets the name of the file apart in a setup result.</summary>
internal static class SetupMessage
{
    public static IReadOnlyList<DoctorTextPart> WithFileName(string message, string name)
    {
        var start = message.IndexOf(name, StringComparison.Ordinal);
        if(start < 0)
            return [message];

        return [message[..start], DoctorTextPart.CodeText(name), message[(start + name.Length)..]];
    }
}
