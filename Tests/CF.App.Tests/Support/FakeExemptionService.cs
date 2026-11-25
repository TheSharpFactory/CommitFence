using CF.Core.Code;
using CF.Core.Services;

namespace CF.App.Tests.Support;

/// <summary>
/// The scan of the tests: it answers what the test sets, on the thread that reads, and can be held there until the test lets it go.
/// </summary>
internal sealed class FakeExemptionService : IExemptionService
{
    public Func<ExemptionScan> Answer { get; set; } = ExemptionSamples.Picture;

    /// <summary>
    /// When set, a scan waits for it before it answers.
    /// </summary>
    public ManualResetEventSlim? Gate { get; set; }

    public int Scans { get; private set; }

    public ExemptionScan Scan(string folder, bool withLines, CancellationToken cancellationToken)
    {
        Scans++;
        Gate?.Wait(cancellationToken);
        return Answer();
    }
}

/// <summary>
/// The exemptions the screen is drawn with: twenty-eight lines that set eight rules aside, in seventeen files, two of them with no reason.
/// </summary>
internal static class ExemptionSamples
{
    private const string _copied = "Copied from Sharp Factory's updater, kept as it is there";
    private const string _internal = "Internal so the updater tests can swap the feed";

    private static readonly (string File, int Line, string Rule, string? Reason)[] _lines =
    [
        ("Dependencies/CF.Adapters/Updates/ReleaseUpdater.cs", 208, "TEAM001", _copied),
        ("Dependencies/CF.Adapters/Updates/ReleaseUpdater.cs", 241, "TEAM001", _copied),
        ("Dependencies/CF.Adapters/Updates/ReleaseUpdater.cs", 263, "TEAM001", _copied),
        ("Dependencies/CF.Adapters/Updates/ReleaseUpdater.cs", 302, "TEAM001", _copied),
        ("Dependencies/CF.Adapters/Updates/ReleaseUpdater.cs", 87, "TEAM011", _internal),
        ("App/CF.App/Rules/RulesViewModel.FileRules.cs", 31, "CA1822", "Kept for the plug-in contract"),
        ("App/CF.App/Rules/RulesViewModel.FileRules.cs", 58, "CA1822", "Kept for the plug-in contract"),
        ("App/CF.App/Rules/RulesViewModel.FileRules.cs", 94, "CA1822", "The binding reads it on an instance"),
        ("Dependencies/CF.Core/Review/ExemptionFilter.cs", 14, "TEAM011", "A test double reaches it"),
        ("Dependencies/CF.Core/Review/ExemptionFilter.cs", 40, "TEAM013", "Reads the text it is given, not a file"),
        ("App/CF.App/Review/SentencePieces.axaml.cs", 22, "CA1822", "The view binds it"),
        ("App/CF.App/Review/SentencePieces.axaml.cs", 31, "IDE0051", "Used by the template"),
        ("Tests/CF.App.Tests/Support/Doubles.cs", 120, "CA1822", "A double keeps the shape of what it replaces"),
        ("Tests/CF.App.Tests/Support/Doubles.cs", 133, "CA1051", "A test double is read directly"),
        ("Dependencies/CF.Core/Updates/ReleaseNotes.cs", 18, "TEAM013", "Pure text, no file is touched"),
        ("Dependencies/CF.Core/Updates/ReleaseNotes.cs", 77, "TEAM013", "Pure text, no file is touched"),
        ("App/CF.App/Settings/MarkedText.cs", 63, "IDE0055", "Aligned to read as a table"),
        ("App/CF.App/Settings/MarkedText.cs", 91, "IDE0051", "Used by the style"),
        ("Dependencies/CF.Core/Review/SettingsInTree.cs", 52, "TEAM013", "Reads the text it is given"),
        ("App/CF.Cli/Commands/ReviewArguments.cs", 110, "TEAM001", "One line reads better here"),
        ("Dependencies/CF.Adapters/Updates/UpdateInstaller.cs", 55, "TEAM001", _copied),
        ("App/CF.App/Review/ReviewViewModel.cs", 412, "TEAM001", null),
        ("Dependencies/CF.Adapters/Git/GitRunner.cs", 22, "TEAM011", null),
        ("App/CF.App/Shell/Program.cs", 14, "CA1822", "The entry point is called by the runtime"),
        ("App/CF.Cli/Commands/UsageText.cs", 9, "IDE0055", "Aligned to read as a list"),
        ("Dependencies/CF.Core/Services/StructureService.cs", 71, "TEAM012", "The judgment is stamped with the time it ended"),
        ("App/CF.App/Updates/UpdatesViewModel.cs", 120, "TEAM012", "A timer asks the system for the hour"),
        ("Dependencies/CF.Core/Code/ArtifactReach.cs", 28, "CA1051", "A record of two fields")
    ];

    /// <summary>
    /// The scan the pictures of the screen are made from, on main, at the commit 4f2a91c.
    /// </summary>
    public static ExemptionScan Picture()
    {
        return ExemptionScan.Of("main", "4f2a91c".PadRight(40, '0'), _lines.Select(Entry).ToList());
    }

    /// <summary>
    /// A scan of this many allow comments in this many files, each with a reason: more than the screen opens in full.
    /// </summary>
    public static ExemptionScan Many(int files, int perFile)
    {
        var entries = Enumerable.Range(0, files).SelectMany(file => Enumerable.Range(1, perFile).Select(at => Entry(($"src/Area{file / 10}/File{file:000}.cs", (at * 10) + 5, "TEAM001", "Kept as it was"))));
        return ExemptionScan.Of("main", "4f2a91c".PadRight(40, '0'), entries.ToList());
    }

    /// <summary>
    /// A scan that found nothing in the last commit.
    /// </summary>
    public static ExemptionScan None()
    {
        return ExemptionScan.Of("main", "4f2a91c".PadRight(40, '0'), []);
    }

    private static ExemptionEntry Entry((string File, int Line, string Rule, string? Reason) line)
    {
        var form = line.Rule.StartsWith("TEAM", StringComparison.Ordinal) ? ExemptionForm.AllowComment : (line.Line % 2 == 0 ? ExemptionForm.Pragma : ExemptionForm.SuppressMessage);
        var finding = form == ExemptionForm.AllowComment && line.Reason == null;
        return new ExemptionEntry(line.File, line.Line, line.Rule, form, line.Reason, finding, LinesAround(line, form));
    }

    private static List<FileLine> LinesAround((string File, int Line, string Rule, string? Reason) line, ExemptionForm form)
    {
        var text = form switch
        {
            ExemptionForm.Pragma => $"        #pragma warning disable {line.Rule}" + (line.Reason == null ? string.Empty : " // " + line.Reason),
            ExemptionForm.SuppressMessage => $"        [SuppressMessage(\"Design\", \"{line.Rule}\", Justification = \"{line.Reason}\")]",
            ExemptionForm.AllowComment => $"        // commitfence:allow {line.Rule}" + (line.Reason == null ? string.Empty : ": " + line.Reason),
            _ => string.Empty
        };
        var code = new[] { "    {", "        var lockPath = Path.Combine(downloadFolder, \"update.lock\");", string.Empty, text, "        if(File.Exists(lockPath))", "        {", "            return UpdateResult.Busy;" };
        return code.Select((content, index) => new FileLine(line.Line - 3 + index, content)).ToList();
    }
}
