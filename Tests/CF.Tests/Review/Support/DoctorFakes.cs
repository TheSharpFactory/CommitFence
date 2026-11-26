using CF.Core.Ports;

namespace CF.Tests.Review.Support;

/// <summary>The paths and versions returned by the Doctor ports.</summary>
internal sealed class DoctorGitStub(string folder) : IDoctorGit
{
    public string? ProgramSetting { get; init; }

    public string ReadVersion()
    {
        return "2.47.1";
    }

    public RepositoryLocation FindRepository(string folder)
    {
        return new RepositoryLocation(folder, Path.Combine(folder, ".git"));
    }

    public string? HooksFolder { get; init; }

    public string FindHook(RepositoryLocation repository, string hook)
    {
        return Path.Combine(HooksFolder ?? Path.Combine(repository.GitDirectory, "hooks"), hook);
    }

    public string? ReadHookSetting(RepositoryLocation repository, string key)
    {
        return key == "core.hooksPath" ? HooksFolder : ProgramSetting;
    }

    public RepositoryLocation Repository
    {
        get { return FindRepository(folder); }
    }
}

internal sealed class DoctorMachineStub(string programPath) : IDoctorMachine
{
    public string ProgramPath { get; } = programPath;

    public string Version { get; init; } = FixedProduct.ProgramVersion;
    public string? ComponentVersion { get; init; }

    public string FindOnPath(string name)
    {
        return ProgramPath;
    }

    public bool SamePath(string left, string right)
    {
        return Path.GetFullPath(left) == Path.GetFullPath(right);
    }

    public string ReadVersion(string path)
    {
        return Path.GetFileName(path) == "CF.Meaning.dll" ? ComponentVersion ?? Version : Version;
    }

    public bool IsExecutable(string path)
    {
        return true;
    }
}
