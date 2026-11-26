namespace CF.Core.Setup;

/// <summary>Actions a face can offer for a Doctor result.</summary>
public enum DoctorActionKind
{
    /// <summary>Open a file at the line in the action.</summary>
    OpenFile,

    /// <summary>Copy the action's text.</summary>
    CopyText,

    /// <summary>Open the action's web address.</summary>
    OpenWeb,

    /// <summary>Open a directory in the file manager.</summary>
    OpenFolder
}
