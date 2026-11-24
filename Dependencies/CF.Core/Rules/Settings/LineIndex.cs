using System.Text;

namespace CF.Core.Rules.Settings;

/// <summary>
/// Turns a byte offset in UTF-8 text into a line and a column, the column counted in characters.
/// </summary>
internal sealed class LineIndex
{
    private readonly byte[] _bytes;
    private readonly List<int> _lineStarts = new List<int> { 0 };

    public LineIndex(byte[] bytes)
    {
        _bytes = bytes;
        for(var offset = 0; offset < bytes.Length; offset++)
        {
            if(bytes[offset] == (byte)'\n')
                _lineStarts.Add(offset + 1);
        }
    }

    public TextPosition PositionOfOffset(long byteOffset)
    {
        var offset = (int)Math.Min(byteOffset, _bytes.Length);
        var lineIndex = _lineStarts.BinarySearch(offset);
        if(lineIndex < 0)
            lineIndex = ~lineIndex - 1;

        return PositionInLine(lineIndex, offset - _lineStarts[lineIndex]);
    }

    public TextPosition PositionOfLine(long zeroBasedLine, long bytePositionInLine)
    {
        var lineIndex = (int)Math.Min(zeroBasedLine, _lineStarts.Count - 1);
        var lineStart = _lineStarts[lineIndex];
        var offset = (int)Math.Min(lineStart + bytePositionInLine, _bytes.Length);
        return PositionInLine(lineIndex, offset - lineStart);
    }

    private TextPosition PositionInLine(int lineIndex, int bytesIntoLine)
    {
        var characters = Encoding.UTF8.GetCharCount(_bytes, _lineStarts[lineIndex], bytesIntoLine);
        return new TextPosition(lineIndex + 1, characters + 1);
    }
}