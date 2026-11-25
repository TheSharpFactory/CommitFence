namespace CF.App.Doctor;

/// <summary>
/// A run of checks under one name: "Git and the hook", "C#", "This repository".
/// </summary>
internal sealed class DoctorGroupViewModel
{
    public DoctorGroupViewModel(string name, IReadOnlyList<DoctorCheckViewModel> checks)
    {
        Name = name;
        Checks = checks;
    }

    public string Name { get; }

    public IReadOnlyList<DoctorCheckViewModel> Checks { get; }
}
