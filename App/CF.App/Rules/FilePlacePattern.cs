using Avalonia.Controls;
using CF.App.Rules.Page;

namespace CF.App.Rules;

internal sealed class FilePlacePattern : ShortValueText
{
    public FilePlacePattern()
    {
        ShortenedChanged += () => ToolTip.SetTip(this, IsShortened ? WholeText : null);
    }
}
