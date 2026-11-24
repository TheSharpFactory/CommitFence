namespace CF.App.Check;

/// <summary>
/// What the check program is told on its command line: the command-line program to compare with, the folder for the
/// pictures, and the settings file that holds the repository's rules, which is copied for every case.
/// </summary>
internal sealed class CheckOptions
{
    public CheckOptions(string commitfencePath, string picturesFolder, string sampleSettingsPath)
    {
        CommitfencePath = commitfencePath;
        PicturesFolder = picturesFolder;
        SampleSettingsPath = sampleSettingsPath;
    }

    public string CommitfencePath { get; }

    public string PicturesFolder { get; }

    public string SampleSettingsPath { get; }

    /// <summary>
    /// Reads <c>--commitfence &lt;program&gt; --pictures &lt;folder&gt; --sample &lt;commitfence.json&gt;</c>. Returns nothing, with
    /// the reason, when an option is missing or the files are not there.
    /// </summary>
    public static CheckOptions? Parse(string[] arguments, out string error)
    {
        var commitfence = ValueOf(arguments, "--commitfence");
        var pictures = ValueOf(arguments, "--pictures");
        var sample = ValueOf(arguments, "--sample");
        error = string.Empty;
        if(commitfence == null || pictures == null || sample == null)
        {
            error = "usage: CF.App.Check --commitfence <program> --pictures <folder> --sample <commitfence.json>";
            return null;
        }

        if(!File.Exists(commitfence) || !File.Exists(sample))
        {
            error = "the command-line program or the sample settings file does not exist";
            return null;
        }

        return new CheckOptions(Path.GetFullPath(commitfence), Path.GetFullPath(pictures), Path.GetFullPath(sample));
    }

    private static string? ValueOf(string[] arguments, string name)
    {
        var index = Array.IndexOf(arguments, name);
        if(index < 0 || index + 1 >= arguments.Length)
            return null;

        return arguments[index + 1];
    }
}
