using System.Text.Json;
using System.Text.RegularExpressions;
using CF.Core.Ports;
using CF.Core.Setup;
using static CF.Core.Setup.DoctorTextPart;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>Lets dotnet select the SDK, then explains the result using the repository's global.json.</summary>
internal sealed partial class SdkDoctorCheck(string language, DotnetTool dotnet, IFileSystem files)
    : DoctorCheck(language, "The .NET SDK the repository needs is installed", "find the repository's SDK settings")
{
    private const string _globalJson = "global.json";
    private static readonly DoctorAction _download = new DoctorAction(DoctorActionKind.OpenWeb, "Open the .NET download page", "https://dotnet.microsoft.com/download");

    [GeneratedRegex(@"^(?<framework>\d+\.\d+)\.\d+(?:[-+][\w.+-]+)?$")]
    private static partial Regex SdkVersion();

    protected override DoctorCheckResult Check(DoctorContext context)
    {
        var folder = context.Repository.WorkFolder;
        var selection = new SdkCheck(dotnet).Check(folder);
        var global = FindGlobalJson(folder);
        if(global == null)
            return WithoutGlobalJson(folder, selection);

        var text = files.ReadAllText(global);
        DoctorSdkOptions? sdk;
        try
        {
            sdk = JsonSerializer.Deserialize(text, DoctorGlobalJsonContext.Default.DoctorGlobalJson)?.Sdk;
        }
        catch(JsonException exception)
        {
            var line = exception.LineNumber.HasValue ? checked((int)exception.LineNumber.Value + 1) : (int?)null;
            return FromFile(new DoctorText(CodeText(_globalJson), " could not be read: ", Message(exception)),
                new DoctorText("Correct ", CodeText(_globalJson), ", then check again."), global, text, line, Installed(folder).LastOrDefault());
        }

        if(selection.Problem == null && SdkVersion().IsMatch(selection.Version))
        {
            var found = sdk?.Version == null
                ? new DoctorText(CodeText(_globalJson), " does not ask for a version, so ", CodeText("dotnet"), " selects ", CodeText(selection.Version), ".")
                : new DoctorText(CodeText(_globalJson), " asks for ", CodeText(sdk.Version), " and ", CodeText("dotnet"), " selects ", CodeText(selection.Version), ".");
            return Pass(found, selection.Version);
        }

        return MissingSelection(folder, global, text, sdk);
    }

    private DoctorCheckResult WithoutGlobalJson(string folder, SdkCheckResult selection)
    {
        var version = SdkVersion().Match(selection.Version);
        if(selection.Problem == null && version.Success)
        {
            return Pass(new DoctorText("No ", CodeText(_globalJson), ", so the newest: ", CodeText(selection.Version), ", which builds ",
                CodeText("net" + version.Groups["framework"].Value), "."), selection.Version);
        }

        var installed = Installed(folder);
        return Fail(new DoctorText(installed.Count == 0 ? "No .NET SDK was found." : "dotnet could not select an installed .NET SDK."),
            new DoctorText("Install the .NET SDK the repository uses, then check again."), DoctorImpact.Sdk,
            [_download], sdkVersion: installed.LastOrDefault());
    }

    private DoctorCheckResult MissingSelection(string folder, string global, string text, DoctorSdkOptions? sdk)
    {
        var installed = Installed(folder);
        var available = installed.Count switch
        {
            0 => "no installed SDK was found.",
            1 => "this machine has " + installed[0] + " only.",
            _ => "this machine has " + string.Join(", ", installed) + "."
        };
        var requested = sdk?.Version;
        if(requested == null)
        {
            return FromFile(new DoctorText(CodeText("dotnet"), " could not select an SDK using ", CodeText(_globalJson), "."),
                new DoctorText("Check the SDK settings in ", CodeText(_globalJson), " and install an SDK the repository uses."), global, text, null, installed.LastOrDefault());
        }

        var policy = string.Equals(sdk?.RollForward, "disable", StringComparison.OrdinalIgnoreCase)
            ? " and does not roll forward; "
            : ", but no SDK was selected; ";
        var found = new DoctorText(CodeText(_globalJson), " asks for ", CodeText(requested), policy, CodeText(available));
        var fix = new DoctorText("Install the .NET SDK ", CodeText(requested), ", or let ", CodeText(_globalJson),
            " roll forward to a newer one. CommitFence does not change ", CodeText(_globalJson), ".");
        return FromFile(found, fix, global, text, FindVersionLine(text, requested), installed.LastOrDefault());
    }

    private DoctorCheckResult FromFile(DoctorText found, DoctorText fix, string path, string text, int? line, string? version)
    {
        var actions = new List<DoctorAction> { new DoctorAction(DoctorActionKind.OpenFile, "Open global.json", path, line), _download };
        return Fail(found, fix, DoctorImpact.Sdk, actions, DoctorExcerpt.Around(path, DoctorExcerpt.ReadLines(text), line), version);
    }

    private string? FindGlobalJson(string folder)
    {
        for(var parent = (string?)folder; parent != null; parent = Path.GetDirectoryName(parent))
        {
            var path = Path.Combine(parent, _globalJson);
            if(files.FileExists(path))
                return path;
        }

        return null;
    }

    private List<string> Installed(string folder)
    {
        var list = dotnet.Run(folder, new List<string> { "--list-sdks" }, DotnetTimeLimits.SdkQuestion);
        if(list.ExitCode != 0)
            return [];

        return list.Output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', 2)[0]).Where(version => SdkVersion().IsMatch(version)).ToList();
    }

    private static int? FindVersionLine(string text, string version)
    {
        var lines = DoctorExcerpt.ReadLines(text);
        var value = "\"" + version + "\"";
        for(var index = 0; index < lines.Count; index++)
        {
            if(lines[index].TrimStart().StartsWith("\"version\"", StringComparison.Ordinal) && lines[index].Contains(value, StringComparison.Ordinal))
                return index + 1;
        }

        return null;
    }
}
