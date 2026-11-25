using CF.Core.Ports;
using CF.Core.Setup;
using static CF.Core.Setup.DoctorTextPart;

namespace CF.Languages.CSharp.Meaning;

/// <summary>Checks the compiler analyzer shipped with the CLI installation.</summary>
internal sealed class MeaningDoctorCheck(string language, IFileSystem files)
    : DoctorCheck(language, "The meaning component matches the program", "identify the repository to check")
{
    protected override DoctorCheckResult Check(DoctorContext context)
    {
        var configured = context.Git.ReadHookSetting(context.Repository, HookLine.PathSetting);
        var useConfigured = !string.IsNullOrWhiteSpace(configured) && files.FileExists(configured);
        var programPath = useConfigured ? configured! : context.ProgramPath;
        var programVersion = useConfigured ? context.Machine.ReadVersion(programPath) : context.ProgramVersion;
        var path = MeaningAnalyzerFile.PathBeside(Path.GetDirectoryName(programPath) ?? string.Empty);
        var name = CodeText(Path.GetFileName(path));
        var fix = new DoctorText("Unpack the complete ", CodeText("CommitFence"), " download again, then check again.");
        if(!files.FileExists(path))
            return Fail(new DoctorText(name, " is missing from ", CodeText(path), "."), fix);

        var version = context.Machine.ReadVersion(path);
        var component = version == null ? null : ProductVersion.TryParseOwn(version);
        var program = programVersion == null ? null : ProductVersion.TryParseOwn(programVersion);
        if(component == null || program == null)
            return Fail(new DoctorText("The version match between ", name, " and the program could not be verified."), fix);

        if(component.CompareTo(program) != 0)
            return Fail(new DoctorText(name, " beside the program is ", CodeText(version!), ", the program ", CodeText(programVersion!), "."), fix);

        return Pass(new DoctorText(name, " beside the program is ", CodeText(version!), "."));
    }
}
