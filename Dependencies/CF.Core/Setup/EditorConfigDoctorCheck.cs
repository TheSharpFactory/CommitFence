using System.Text.RegularExpressions;
using CF.Core.Ports;
using static CF.Core.Setup.DoctorTextPart;

namespace CF.Core.Setup;

/// <summary>Finds the root .editorconfig without invoking a language toolchain.</summary>
internal sealed partial class EditorConfigDoctorCheck(IFileSystem files)
    : DoctorCheck("This repository", ".editorconfig is found", "find the repository root")
{
    private const string _name = ".editorconfig";

    [GeneratedRegex(@"^\s*[\w.-]+\.severity\s*[:=]\s*(?:error|warning|suggestion|silent|none|default|refactoring)(?:\s*(?:[#;].*)?)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeverityLine();

    protected override DoctorCheckResult Check(DoctorContext context)
    {
        var root = context.Repository.WorkFolder;
        var path = Path.Combine(root, _name);
        if(!files.FileExists(path))
        {
            return Fail(new DoctorText("There is no ", CodeText(_name), " at the root of ", CodeText(Path.GetFileName(root)), "."),
                new DoctorText("Add the team's ", CodeText(_name), " at the repository root."));
        }

        var count = DoctorExcerpt.ReadLines(files.ReadAllText(path)).Count(line => SeverityLine().IsMatch(line));
        return Pass(new DoctorText(count == 1 ? "At the root, with 1 line that sets a severity." : $"At the root, with {count} lines that set a severity."));
    }
}
