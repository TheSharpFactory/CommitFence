using System.Diagnostics;

namespace CF.App.Check;

/// <summary>
/// Prints one line per check, PASS or FAIL, with the detail lines of a FAIL under it, and the total at the end, in the
/// form the other check scripts of Tools/checks print.
/// </summary>
internal sealed class CheckReport
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _passed;

    public int Failed { get; private set; }

    /// <summary>
    /// Records one check: the case and what must be seen, whether it was, and what was seen instead when it was not.
    /// </summary>
    public void Check(string caseName, string text, bool passed, params string[] seenInstead)
    {
        if(passed)
        {
            _passed++;
            Console.WriteLine($"PASS  {caseName}: {text}");
            return;
        }

        Failed++;
        Console.WriteLine($"FAIL  {caseName}: {text}");
        foreach(var line in seenInstead)
            Console.WriteLine("        " + line);
    }

    /// <summary>
    /// Records a check that could not even be carried out.
    /// </summary>
    public void Broken(string caseName, string text, Exception exception)
    {
        var where = (exception.StackTrace ?? string.Empty).Split(Environment.NewLine).Take(3).Select(line => line.Trim());
        Check(caseName, text, false, new[] { "the check stopped: " + exception.GetType().Name + ": " + exception.Message }.Concat(where).ToArray());
    }

    /// <summary>
    /// Prints the total and says how the program should end.
    /// </summary>
    public int Finish()
    {
        Console.WriteLine();
        Console.WriteLine($"TOTAL: {_passed} passed, {Failed} failed, 0 skipped, {(int)_clock.Elapsed.TotalSeconds} seconds");
        return Failed == 0 ? 0 : 1;
    }
}
