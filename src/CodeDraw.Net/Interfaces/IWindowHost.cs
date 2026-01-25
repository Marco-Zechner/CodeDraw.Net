using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Interfaces;

public unsafe interface IWindowHost
{
    static IWindowHost Instance { get; }  // TODO: nullable?
    Glfw Glfw { get; }  // you already exposed it; ok to keep here
    DateTime StartTimeUtc { get; }
    void EnsureStarted();

    WindowHandle* CreateWindow(int w, int h, string title);
    void DestroyWindowAndMaybeStop(WindowHandle* win);

    // wire window → event sink (so Impl never references CodeDrawWindow directly)
    void OnWindowCreated(WindowHandle* win, IWindowEventSink sink);
    void OnWindowDestroyed(WindowHandle* win);

    // API
    void SetWindowShouldClose(WindowHandle* win, bool shouldClose);
    void CloseAllWindows();
    void ResizeWindow(WindowHandle* win, int width, int height);

    // metrics for CodeDraw facade
    double HostJobsPerSec { get; }
    double HostBusyPercent { get; }
    double HostIdleSec { get; }

    ILayerMetricsProvider LayerMetrics { get; }
}