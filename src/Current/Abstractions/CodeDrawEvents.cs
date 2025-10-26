namespace MarcoZechner.CodeDrawDotNet;

/// <summary>
/// Global engine events. Subscribe to drive your app without writing your own loop.
/// </summary>
public static class CodeDrawEvents
{
    /// <summary>
    /// Fired on the shared layer-render thread once per tick, just before any shared layers render.
    /// Use this to update world state and record layer commands. <c>dt</c> is seconds since last Update.
    /// </summary>
    public static event Action<IGraphics, double>? Update;

    /// <summary>
    /// Fired when a window's GL context is created and ready, on that window's render thread.
    /// Subscribe per-instance (preferred) or via this global hook to initialize per-window resources.
    /// </summary>
    public static event Action<CodeDrawWindow, IGraphics>? OnWindowLoaded;

    /// <summary>
    /// Fired before any given window renders a frame, on that window's render thread.
    /// Runs before the window's instance-level <see cref="CodeDrawWindow.BeforeRender"/> event.
    /// <c>dt</c> is seconds since this window's previous frame.
    /// </summary>
    public static event Action<CodeDrawWindow, IGraphics, double>? BeforeAnyWindowRender;

    // Internal raisers (engine calls these)
    internal static void RaiseUpdate(IGraphics gfx, double dt) => Update?.Invoke(gfx, dt);
    internal static void RaiseOnWindowLoaded(CodeDrawWindow w, IGraphics gfx) => OnWindowLoaded?.Invoke(w, gfx);
    internal static void RaiseBeforeAnyWindowRender(CodeDrawWindow w, IGraphics gfx, double dt)
        => BeforeAnyWindowRender?.Invoke(w, gfx, dt);
}