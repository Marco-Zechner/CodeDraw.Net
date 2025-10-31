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

    public static EventDrivenMetrics EventLoopUPS
    {
        get
        {
            var host = CodeDrawHost.Instance;
            host.EnsureStarted();
            return new EventDrivenMetrics
            {
                JobsPerSec   = host.HostJobsPerSec,
                BusyPercent  = host.HostBusyPercent,
                IdleSec      = host.HostIdleSec
            };
        }
    }

    public static EventDrivenMetrics LayerWorkerMetrics
    {
        get
        {
            var host = CodeDrawHost.Instance;
            host.EnsureStarted();
            var lw = host.Layers;
            return new EventDrivenMetrics
            {
                JobsPerSec   = lw.JobsPerSec,
                BusyPercent  = lw.BusyPercent,
                IdleSec      = lw.IdleSec
            };
        }
    }
}