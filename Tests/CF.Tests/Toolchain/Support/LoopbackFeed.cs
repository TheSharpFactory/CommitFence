using System.Net;
using System.Net.Sockets;

namespace CF.Tests.Toolchain.Support;

/// <summary>
/// A package feed address on a loopback port the system chose, with a count of the connections made to it: a restore that
/// touches the network shows up here. The feed never answers; a connection is closed as soon as it has been counted.
/// </summary>
internal sealed class LoopbackFeed : IDisposable
{
    private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
    private readonly Task _accepting;
    private int _connections;

    public LoopbackFeed()
    {
        _listener.Start();
        _accepting = Task.Run(AcceptAsync);
    }

    /// <summary>
    /// The connections accepted since the start or since <see cref="Forget"/>.
    /// </summary>
    public int Connections
    {
        get { return Volatile.Read(ref _connections); }
    }

    /// <summary>
    /// The address of the feed's index: a secure one, so that the package tool does not refuse it before it connects.
    /// </summary>
    public string Address
    {
        get { return $"https://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/index.json"; }
    }

    public void Forget()
    {
        Interlocked.Exchange(ref _connections, 0);
    }

    public void Dispose()
    {
        _listener.Stop();
        _accepting.Wait();
    }

    private async Task AcceptAsync()
    {
        while(true)
        {
            try
            {
                using(await _listener.AcceptTcpClientAsync())
                    Interlocked.Increment(ref _connections);
            }
            catch(Exception exception) when(exception is SocketException || exception is ObjectDisposedException || exception is InvalidOperationException)
            {
                return;
            }
        }
    }
}
