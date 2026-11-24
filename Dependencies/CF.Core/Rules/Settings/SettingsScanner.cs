using System.Text.Json;

namespace CF.Core.Rules.Settings;

/// <summary>
/// Walks the settings text once, before anything is read from it.
/// </summary>
/// <remarks>
/// It refuses broken JSON and a key given twice in one object, and it notes where everything starts so that later
/// problems can name a line and a column.
/// </remarks>
internal sealed class SettingsScanner
{
    private readonly byte[] _bytes;
    private readonly LineIndex _lines;
    private readonly Dictionary<string, TextPosition> _positions = new Dictionary<string, TextPosition>();
    private readonly List<(string Path, TextPosition Position)> _nulls = new List<(string Path, TextPosition Position)>();
    private readonly Stack<ScanFrame> _frames = new Stack<ScanFrame>();

    public SettingsScanner(byte[] bytes)
    {
        _bytes = bytes;
        _lines = new LineIndex(bytes);
    }

    public JsonPositions Scan()
    {
        var positions = new JsonPositions(_lines, _positions, _nulls);
        try
        {
            ReadAllTokens();
        }
        catch(JsonException exception)
        {
            throw positions.ErrorAtLine(exception.LineNumber ?? 0, exception.BytePositionInLine ?? 0, "The file is not valid JSON: " + FirstSentence(exception.Message));
        }

        return positions;
    }

    private static string FirstSentence(string message)
    {
        var end = message.IndexOf(". ", StringComparison.Ordinal);
        if(end < 0)
            return message;

        return message.Substring(0, end + 1);
    }

    private void ReadAllTokens()
    {
        var reader = new Utf8JsonReader(_bytes);
        while(reader.Read())
            HandleToken(ref reader);
    }

    private void HandleToken(ref Utf8JsonReader reader)
    {
        switch(reader.TokenType)
        {
            case JsonTokenType.PropertyName:
                HandlePropertyName(ref reader);
                break;
            case JsonTokenType.StartObject:
                _frames.Push(new ScanFrame(BeginValue(ref reader), false));
                break;
            case JsonTokenType.StartArray:
                _frames.Push(new ScanFrame(BeginValue(ref reader), true));
                break;
            case JsonTokenType.EndObject:
            case JsonTokenType.EndArray:
                _frames.Pop();
                break;
            case JsonTokenType.Null:
                _nulls.Add((BeginValue(ref reader), _lines.PositionOfOffset(reader.TokenStartIndex)));
                break;
            default:
                BeginValue(ref reader);
                break;
        }
    }

    private void HandlePropertyName(ref Utf8JsonReader reader)
    {
        var name = reader.GetString() ?? string.Empty;
        var frame = _frames.Peek();
        var position = _lines.PositionOfOffset(reader.TokenStartIndex);
        if(!frame.Keys.Add(name))
            throw new SettingsException($"The key '{name}' is given twice in the same object.", position.Line, position.Column);

        frame.CurrentPropertyPath = JsonPositions.Join(frame.Path, name);
        _positions[frame.CurrentPropertyPath] = position;
    }

    private string BeginValue(ref Utf8JsonReader reader)
    {
        var position = _lines.PositionOfOffset(reader.TokenStartIndex);
        if(_frames.Count == 0)
        {
            _positions[string.Empty] = position;
            return string.Empty;
        }

        var frame = _frames.Peek();
        if(!frame.IsArray)
            return frame.CurrentPropertyPath;

        var path = frame.Path + "[" + frame.NextIndex + "]";
        frame.NextIndex++;
        _positions[path] = position;
        return path;
    }
}