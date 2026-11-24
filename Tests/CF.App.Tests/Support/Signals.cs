using System.ComponentModel;
using CF.App.Rules;
using CF.App.Rules.Page;

namespace CF.App.Tests.Support;

/// <summary>
/// Waits for something the interface does by itself, by the signal it gives: never for a length of time. The limit is only the
/// end of a test that would otherwise hang.
/// </summary>
internal static class Signals
{
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Completes when the condition holds, checked now and after each change of a property of the source.
    /// </summary>
    public static Task Until(INotifyPropertyChanged source, Func<bool> condition)
    {
        if(condition())
            return Task.CompletedTask;

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check(object? sender, PropertyChangedEventArgs e)
        {
            if(!condition())
                return;

            source.PropertyChanged -= Check;
            done.TrySetResult();
        }

        source.PropertyChanged += Check;
        return done.Task.WaitAsync(_limit);
    }

    /// <summary>
    /// Completes the next time the rules of .editorconfig have been listed and are in the list. Ask before the listing is started.
    /// </summary>
    public static Task FileRulesArrived(RulesViewModel rules)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Arrived()
        {
            rules.FileRulesArrived -= Arrived;
            done.TrySetResult();
        }

        rules.FileRulesArrived += Arrived;
        return done.Task.WaitAsync(_limit);
    }

    /// <summary>
    /// Completes the next time Try it shows an answer. Ask before the answer is asked for.
    /// </summary>
    public static Task Shown(TryItViewModel tryIt)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Showed()
        {
            tryIt.Shown -= Showed;
            done.TrySetResult();
        }

        tryIt.Shown += Showed;
        return done.Task.WaitAsync(_limit);
    }

    /// <summary>
    /// Completes when Try it is not running a rule any more (at once when it is not).
    /// </summary>
    public static Task Idle(TryItViewModel tryIt)
    {
        return Until(tryIt, () => !tryIt.IsBusy);
    }
}
