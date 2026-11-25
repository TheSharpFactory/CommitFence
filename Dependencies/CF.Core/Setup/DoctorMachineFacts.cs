namespace CF.Core.Setup;

/// <summary>What Doctor could identify about the current machine.</summary>
/// <param name="ProgramPath">The running product's command-line path.</param>
/// <param name="ProgramVersion">The running product's version.</param>
/// <param name="GitVersion">The Git version, when found.</param>
/// <param name="SdkVersion">The selected SDK version, or the newest installed version found.</param>
/// <param name="DataFolder">Where the product keeps its local data, when available.</param>
public sealed record DoctorMachineFacts(string ProgramPath, string ProgramVersion, string? GitVersion, string? SdkVersion, string? DataFolder);
