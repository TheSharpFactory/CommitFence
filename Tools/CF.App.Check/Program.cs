using CF.Composition;

namespace CF.App.Check;

/// <summary>
/// The check program of the app: opens the real main window headless, saves pictures of it, and checks it against the
/// command-line program. It prints one PASS or FAIL line per check and the total, and ends non-zero when any check fails.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = CheckOptions.Parse(args, out var error);
        if(options == null)
        {
            Console.Error.WriteLine(error);
            return 64;
        }

        using var folders = new SampleFolders(options, new CommandLine(options.CommitfencePath));
        Environment.SetEnvironmentVariable("COMMITFENCE_DATA_FOLDER", folders.DataFolder);

        // the window looks for a newer version when it opens: here that goes to a port nothing listens on, never to the network
        Environment.SetEnvironmentVariable("COMMITFENCE_UPDATE_FEED", "http://127.0.0.1:9/latest.json");
        var report = new CheckReport();
        var context = new CheckContext(options, new CommandLine(options.CommitfencePath), folders, CompositionRoot.Create(), report);
        var only = Environment.GetEnvironmentVariable("CF_CHECK_ONLY");
        if(only == "review")
        {
            new ReviewChecks(context).Run();
            return report.Finish();
        }

        if(only == "update")
        {
            new UpdateLookChecks(context).Run();
            return report.Finish();
        }

        if(only == "file")
        {
            new FileRuleChecks(context).Run();
            return report.Finish();
        }

        if(string.IsNullOrEmpty(only))
        {
            new RuleListChecks(context).Run();
            new RuleChangeChecks(context).Run();
            new PageEditChecks(context).Run();
            new LookChecks(context).Run();
            new PageLookChecks(context).Run();
            new FileRuleChecks(context).Run();
            new ReviewChecks(context).Run();
            new UpdateLookChecks(context).Run();
        }

        new TryItChecks(context).Run();

        return report.Finish();
    }
}
