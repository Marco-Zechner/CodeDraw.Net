namespace MarcoZechner.CodeDrawDotNet;

public static class CodeDrawHost
{
    private static readonly Lock _gate = new();
    private static CodeDrawApp? _current;

    public static CodeDrawApp Started()
    {
        lock (_gate)
        {
            if (_current is { IsDisposed: false })
                throw new InvalidOperationException("CodeDrawHost is already running...");

            var host = new SharedGlfwHost();
            host.Start();

            _current = new CodeDrawApp(host, onDispose: () =>
            {
                lock (_gate) _current = null;
            });

            return _current;
        }
    }

    internal static SharedGlfwHost RequireRunningHost()
    {
        lock (_gate)
        {
            if (_current == null || _current.IsDisposed)
                throw new InvalidOperationException("CodeDrawHost.Started() must be called...");
            return _current.Host;
        }
    }

    internal static CodeDrawApp RequireRunningApp()
    {
        lock (_gate)
        {
            if (_current == null || _current.IsDisposed)
                throw new InvalidOperationException("CodeDrawHost.Started() must be called...");
            return _current;
        }
    }

    public static CodeDrawApp Current
    {
        get
        {
            lock (_gate)
            {
                if (_current == null || _current.IsDisposed)
                    throw new InvalidOperationException("No running CodeDrawApp...");
                return _current;
            }
        }
    }
}