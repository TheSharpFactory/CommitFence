using CF.Core.Ports;
using CF.Core.Rules.Settings;
using CF.Core.Setup;
using static CF.Core.Setup.DoctorTextPart;

namespace CF.Core.Services;

/// <summary>Checks the program selected by the repository's effective Git configuration.</summary>
internal sealed class HookProgramDoctorCheck(IDoctorGit git, IFileSystem files, SettingsFile settings)
    : DoctorCheck("Git and the hook", "Git finds the program", "find the program")
{
    protected override DoctorCheckResult Check(DoctorContext context)
    {
        var key = CodeText(HookLine.PathSetting);
        var path = git.ReadHookSetting(context.Repository, HookLine.PathSetting);
        if(string.IsNullOrWhiteSpace(path))
            return Install(new DoctorText(key, " is not set."));

        if(!Path.IsPathFullyQualified(path))
            return Install(new DoctorText(key, " points at ", CodeText(path), ", which is not a full path."));

        if(!files.FileExists(path))
            return Install(new DoctorText(key, " points at ", CodeText(path), ", which is not there."));

        if(!context.Machine.IsExecutable(path))
            return Install(new DoctorText(key, " points at ", CodeText(path), ", which is not executable."));

        var version = context.Machine.ReadVersion(path);
        if(version == null || ProductVersion.TryParseOwn(version) == null)
            return Install(new DoctorText(key, " points at ", CodeText(path), ", whose version could not be read."));

        return CheckRequirement(context.Repository.WorkFolder, path, version);
    }

    private DoctorCheckResult CheckRequirement(string repositoryFolder, string programPath, string version)
    {
        var path = SettingsFileName.PathIn(repositoryFolder);
        try
        {
            var text = files.FileExists(path) ? settings.ReadText(path) : null;
            var requires = text == null ? null : SettingsReader.ReadRequires(text);
            var found = new DoctorText(CodeText(HookLine.PathSetting), " is ", CodeText(programPath), ", version ", CodeText(version),
                requires == null ? ". The repository accepts any version." : ". The repository asks for ",
                requires == null ? "" : CodeText(requires), requires == null ? "" : " or later.");
            if(VersionRequirement.Refusal(requires, version) != null)
                return Fail(found, new DoctorText("Update ", CodeText("CommitFence"), ", then run ", CodeText("commitfence install"), " from the updated copy."), DoctorImpact.Program);

            if(text == null)
                return Pass(found);

            var higher = settings.HigherFeatureVersion(settings.Parse(text));
            if(higher == null)
                return Pass(found);

            var actions = new List<DoctorAction> { new DoctorAction(DoctorActionKind.OpenFile, "Open commitfence.json", path, null) };
            return Fail(new DoctorText(CodeText(SettingsFileName.Default), " uses rule features that require CommitFence ", CodeText(higher),
                requires == null ? ". The file has no requires setting." : $". The file requires only {requires}."),
                new DoctorText("Set the minimum version in ", CodeText(SettingsFileName.Default), " to ", CodeText($"\"requires\": \"{higher}\""), "."),
                DoctorImpact.Program, actions);
        }
        catch(SettingsException exception)
        {
            var excerpt = SettingsExcerpt(path, exception.Line);
            var actions = new List<DoctorAction> { new DoctorAction(DoctorActionKind.OpenFile, "Open commitfence.json", path, exception.Line) };
            return Fail(new DoctorText("The repository settings could not be read: ", CodeText(SettingsFileName.Default), ": ", Message(exception)),
                new DoctorText("Correct ", CodeText(SettingsFileName.Default), ", then check again."), DoctorImpact.Program, actions, excerpt);
        }
    }

    private DoctorExcerpt SettingsExcerpt(string path, int? line)
    {
        try
        {
            return DoctorExcerpt.Around(path, DoctorExcerpt.ReadLines(files.ReadAllText(path)), line);
        }
        catch(Exception exception) when(IsMachineFailure(exception))
        {
            return new DoctorExcerpt(path, line, []);
        }
    }

    private DoctorCheckResult Install(DoctorText found)
    {
        return Fail(found, new DoctorText("Run ", CodeText("commitfence install"), " from the copy you want Git to use."), DoctorImpact.Program);
    }
}
