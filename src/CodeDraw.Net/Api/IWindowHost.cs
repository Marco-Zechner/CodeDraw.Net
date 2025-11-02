using MarcoZechner.CodeDrawDotNet.Api.Events;
using MarcoZechner.CodeDrawDotNet.Engine.Abstractions;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Api;

public unsafe interface IWindowHost
{
    Glfw Glfw { get; }  // you already exposed it; ok to keep here
    void EnsureStarted();

    WindowHandle* CreateWindow(int w, int h, string title);
    void DestroyWindowAndMaybeStop(WindowHandle* win);

    // wire window → event sink (so Impl never references CodeDrawWindow directly)
    void OnWindowCreated(WindowHandle* win, IWindowEventSink sink);
    void OnWindowDestroyed(WindowHandle* win);

    // used by RequestClose in your API
    void SetWindowShouldClose(WindowHandle* win, bool shouldClose);

    // metrics for CodeDraw facade
    double HostJobsPerSec { get; }
    double HostBusyPercent { get; }
    double HostIdleSec { get; }

    ILayerMetricsProvider LayerMetrics { get; }
}