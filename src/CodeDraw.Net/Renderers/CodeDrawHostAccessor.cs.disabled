using MarcoZechner.CodeDrawDotNet.Engine;
using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Renderers;

internal static class CodeDrawHostAccessor
{
    private static IWindowHost? _host;
    public static IWindowHost Host {
        get {
            if (_host != null) return _host;
            // self-bootstrap the default engine:
            _host = CodeDrawHost.Instance;
            _host.EnsureStarted();
            return _host;
        }
    }
}
