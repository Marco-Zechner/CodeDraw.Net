using MarcoZechner.CodeDrawDotNet.Engine;

namespace MarcoZechner.CodeDrawDotNet;

/// <summary>
/// Global engine facade for shared state and metrics.
/// </summary>
public static class CodeDraw
{
    /// <summary>
    /// Uptime since the rendering backend started (hidden share-root + services).
    /// </summary>
    public static TimeSpan EngineUptime
    {
        get
        {
            var host = CodeDrawHost.Instance;
            host.EnsureStarted();
            return DateTime.UtcNow - host.StartTimeUtc;
        }
    }
}