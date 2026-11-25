namespace CF.Core.Ports;

/// <summary>Checks the private checkout without creating, moving or repairing it.</summary>
public interface ISnapshotInspector
{
    /// <summary>Reads the private checkout's health.</summary>
    PrivateCopyStatus Inspect(RepositoryLocation repository);
}
