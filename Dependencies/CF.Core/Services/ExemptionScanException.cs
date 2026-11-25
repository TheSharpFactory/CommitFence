namespace CF.Core.Services;

/// <summary>The last commit cannot be scanned: the folder is not in a repository, or git cannot be asked.</summary>
public sealed class ExemptionScanException : Exception
{
    /// <summary>Creates the exception with the plain reason.</summary>
    public ExemptionScanException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the plain reason and the failure behind it.</summary>
    public ExemptionScanException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
