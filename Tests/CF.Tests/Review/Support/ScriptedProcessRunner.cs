using CF.Core.Ports;

namespace CF.Tests.Review.Support;

/// <summary>
/// A process runner that starts nothing: each request is answered by the first route that matches it, and every request
/// is recorded. A request no route matches is a mistake of the test and throws.
/// </summary>
internal sealed class ScriptedProcessRunner : IProcessRunner
{
    private readonly Lock _syncRoot = new Lock();
    private readonly List<(Func<ProcessRequest, bool> Matches, Func<ProcessRequest, ProcessResult> Answer)> _routes = new List<(Func<ProcessRequest, bool> Matches, Func<ProcessRequest, ProcessResult> Answer)>();
    private readonly List<ProcessRequest> _requests = new List<ProcessRequest>();

    public IReadOnlyList<ProcessRequest> Requests
    {
        get
        {
            lock(_syncRoot)
            {
                return _requests.ToList();
            }
        }
    }

    public ScriptedProcessRunner Route(Func<ProcessRequest, bool> matches, Func<ProcessRequest, ProcessResult> answer)
    {
        _routes.Add((matches, answer));
        return this;
    }

    public ProcessResult Run(ProcessRequest request)
    {
        lock(_syncRoot)
        {
            _requests.Add(request);
        }

        foreach(var route in _routes)
        {
            if(route.Matches(request))
                return route.Answer(request);
        }

        throw new InvalidOperationException($"no answer is scripted for: {request.FileName} {string.Join(' ', request.Arguments)}");
    }

    public static ProcessResult Output(string text)
    {
        return new ProcessResult(0, text, string.Empty);
    }

    public static ProcessResult Failure(int exitCode, string error)
    {
        return new ProcessResult(exitCode, string.Empty, error);
    }
}
