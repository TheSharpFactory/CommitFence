using CF.Core.Fixes;
using CF.Core.Services;

namespace CF.App.Review;

/// <summary>Prepares one file-wide fix at a time, reusing the answer for findings with the same inputs.</summary>
internal sealed class ReviewFixQueue(IFixService service)
{
    private readonly SemaphoreSlim _oneAtATime = new SemaphoreSlim(1);

    public async Task<FixAnswer> PrepareAsync(FixRequest request, Func<bool> fileChanged)
    {
        await _oneAtATime.WaitAsync();
        try
        {
            return fileChanged() ? new FixAnswer() : await Task.Run(() => service.SuggestFix(request));
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    public Task<FixApplied> ApplyAsync(Fix fix)
    {
        return Task.Run(() => service.ApplyFix(fix));
    }
}
