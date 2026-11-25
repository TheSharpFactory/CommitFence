namespace CF.Core.Ports;

/// <summary>Read-only Git queries for Doctor.</summary>
public interface IDoctorGit
{
    /// <summary>Git's version, or nothing when it could not be started.</summary>
    string? ReadVersion();

    /// <summary>The repository the folder belongs to, if any.</summary>
    RepositoryLocation? FindRepository(string folder);

    /// <summary>The effective path for Git's pre-push hook.</summary>
    string FindPrePushHook(RepositoryLocation repository);

    /// <summary>The value of a setting as the repository's hook would read it.</summary>
    string? ReadHookSetting(RepositoryLocation repository, string key);
}
