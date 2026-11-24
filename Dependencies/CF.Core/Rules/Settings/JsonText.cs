using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using CF.Core.Code;

namespace CF.Core.Rules.Settings;

/// <summary>
/// Writes single JSON values as text. Strings go through the source-generated context, with an encoder that leaves
/// characters such as the > of an arrow as they are.
/// </summary>
internal static class JsonText
{
    private static readonly SettingsJsonContext _context = new SettingsJsonContext(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    public static string Quote(string text)
    {
        return JsonSerializer.Serialize(text, _context.String);
    }

    public static string Value(PropertyValue value)
    {
        if(value.Type == PropertyValueType.Text)
            return Quote(value.Text);

        if(value.Type == PropertyValueType.Number)
            return value.Number.ToString(CultureInfo.InvariantCulture);

        return value.Flag ? "true" : "false";
    }

    public static string InlineArray(IEnumerable<string> quotedItems)
    {
        return "[ " + string.Join(", ", quotedItems) + " ]";
    }
}