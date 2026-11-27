using CF.App.Parts;
using Xunit;

namespace CF.App.Tests;

public sealed class PathCutTests
{
    private const string _path = "src/Billing/Invoices/Ledger.cs";

    // Eight units a character: the width in units is the count of characters times eight.
    private static double Measure(string text, bool bold)
    {
        return text.Length * 8;
    }

    [Theory]
    [InlineData(1000, "src/Billing/Invoices/", "Ledger.cs")]
    [InlineData(30 * 8, "src/Billing/Invoices/", "Ledger.cs")]
    [InlineData(29 * 8, "…/Billing/Invoices/", "Ledger.cs")]
    [InlineData(27 * 8, "…/Invoices/", "Ledger.cs")]
    [InlineData(19 * 8, "…/", "Ledger.cs")]
    [InlineData(10 * 8, "", "Ledger.cs")]
    [InlineData(7 * 8, "", "Ledger…")]
    public void ThePathIsCutAtFolderBoundariesFromTheLeftAndTheNameStaysWholeUntilNothingElseFits(double width, string folders, string name)
    {
        var parts = PathCut.Fit(_path, width, Measure);

        Assert.Equal(folders, parts.Folders);
        Assert.Equal(name, parts.Name);
    }
}
