namespace MarcoZechner.CodeDrawDotNet.Engine;

/// <summary>
/// Per-window renderer abstraction. Owns a window context and drives its render loop on a dedicated thread.
/// </summary>
internal interface IWindowRenderer
{
    /// <summary>
    /// Starts the render loop for the specified window. Returns after the thread has been spawned.
    /// </summary>
    /// <param name="windowHandle">Platform window handle.</param>
    /// <param name="title">Window title (for logging/metrics).</param>
    void Start(IntPtr windowHandle, string title);

    /// <summary>
    /// Signals the render loop to stop and waits for the thread to exit.
    /// </summary>
    void Stop();

    /// <summary>Total number of frames presented since start.</summary>
    long Frames { get; }

    /// <summary>Monotonic uptime since the renderer started.</summary>
    TimeSpan Uptime { get; }
}