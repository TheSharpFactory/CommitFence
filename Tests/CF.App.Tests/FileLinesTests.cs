using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using CF.App.Parts;
using CF.App.Tests.Support;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// The lines of a file, as the file rule's page and the screens that quote a file show them.
/// </summary>
public sealed class FileLinesTests
{
    [AvaloniaFact]
    public void FileLinesWrapWithTheirRestSetInFourCharacters()
    {
        var lines = new FileLines
        {
            ItemsSource = new List<FileLineViewModel>
            {
                new FileLineViewModel { Number = 12, Text = "    var total = first + second + third + fourth + fifth + sixth + seventh + eighth + ninth + tenth;", IsPresent = true, IsCurrent = true },
                new FileLineViewModel { Number = 13, Text = "}", IsPresent = true }
            },
            NumberWidth = 44,
            NumberGap = 14
        };
        using var host = new ViewHost(lines, 360, 300);

        var rows = host.FindAll<HangingText>(_ => true);
        Assert.Equal(2, rows.Count);
        var text = string.Concat((rows[0].Inlines ?? []).Select(inline => inline is Run run ? run.Text : "\n"));
        var wrapped = text.Split('\n');

        // it wrapped, nothing was cut, and every row after the first starts 4 characters right of the line's own indent
        Assert.True(wrapped.Length > 1);
        Assert.All(wrapped.Skip(1), row => Assert.StartsWith(new string(' ', 8), row, StringComparison.Ordinal));
        Assert.Equal(rows[0].Line.Replace(" ", string.Empty, StringComparison.Ordinal), string.Concat(wrapped).Replace(" ", string.Empty, StringComparison.Ordinal));
        Assert.Equal("}", string.Concat((rows[1].Inlines ?? []).Select(inline => ((Run)inline).Text)));
    }
}
