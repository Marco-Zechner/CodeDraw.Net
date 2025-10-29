using MarcoZechner.CodeDrawDotNet.Engine;
using MarcoZechner.ColorLib;
using MarcoZechner.Math;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet;

/// <summary>
/// A visible window (with its own render thread) that can draw directly
/// and/or composite shared layers in a chosen order.
/// </summary>
public unsafe sealed class CodeDrawWindow : IDisposable
{
    private AbstractWindowRenderer? _renderer;

    /// <summary>Title shown in the OS window chrome.</summary>
    public string Title { get; }

    /// <summary>
    /// Drawable size in pixels. Safe to set before <see cref="Open"/>; marshalled to the UI thread after.
    /// </summary>
    public Vector2<int> Size { get; set; }

    /// <summary>Whether the window can be resized by the user.</summary>
    public bool Resizable { get; set; } = true;

    /// <summary>Target frames per second. 0 = uncapped.</summary>
    public int TargetFPS { get; set; } = 60;

    /// <summary>Background color used to clear the default framebuffer each frame.</summary>
    public Color ClearColor { get; set; } = Color.BLACK;

    /// <summary>Monotonic time since this window started (after <see cref="Open"/>).</summary>
    public TimeSpan Uptime => _renderer?.Uptime ?? TimeSpan.Zero;

    /// <summary>Total number of frames presented by this window.</summary>
    public long Frames => _renderer?.Frames ?? 0;

    /// <summary>User-defined identifier for convenience (optional).</summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Fired once on the window's render thread after GL context creation
    /// and right before the first frame. Use to create per-window resources.
    /// </summary>
    public event Action<CodeDrawWindow, GL, Glfw, nint>? Loaded;

    public event Action<CodeDrawWindow, double>? Update;

    // Default 10ms. Set to 0 to disable the built-in update loop.
    public int UpdateIntervalMs { get; set; } = 10;

    // Optional: warn if a single render action takes longer than this (ms). 0 = off.
    public int LongActionWarnMs { get; set; } = 16;

    internal void RaiseLoaded(GL gl, Glfw glfw, WindowHandle* window) => Loaded?.Invoke(this, gl, glfw, (nint)window);

    /// <summary>
    /// Creates a new window object. The render thread and GL resources are not created
    /// until <see cref="Open"/> is called.
    /// </summary>
    /// <param name="title">Window title.</param>
    public CodeDrawWindow(string title)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
    }

    /// <summary>
    /// Starts the render thread, creates the GL context, and begins the frame loop.
    /// Triggers <see cref="Loaded"/> once and then <see cref="BeforeRender"/> every frame.
    /// </summary>
    public void Open()
    {
        var host = CodeDrawHost.Instance;
        host.EnsureStarted();

        WindowHandle* handle = host.CreateWindow(Size.X, Size.Y, Title);


        _renderer = new DefaultWindowRenderer(handle!, Title);
        _renderer.BindPublic(this);
        _renderer.Start();

        StartUpdateLoopIfNeeded();
    }

    // Internals
    private Thread? _updateThread;
    private volatile bool _updateRunning;

    // call from Open() after renderer starts:
    private void StartUpdateLoopIfNeeded()
    {
        if (UpdateIntervalMs <= 0 || Update is null) return;

        _updateRunning = true;
        _updateThread = new Thread(UpdateLoop) { IsBackground = true, Name = $"Update-{Title}" };
        _updateThread.Start();
    }

    private void UpdateLoop()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double last = 0;

        while (_updateRunning)
        {
            double now = sw.Elapsed.TotalSeconds;
            double dt = now - last; last = now;

            try { Update?.Invoke(this, dt); }
            catch (Exception ex) { Console.WriteLine($"[Update ERROR] {ex}"); }

            if (UpdateIntervalMs > 0)
                Thread.Sleep(UpdateIntervalMs);
            else
                Thread.Yield();
        }
    }

    public void Dispose() => Stop();

    public void Stop()
    {
        _updateRunning = false;
        _updateThread?.Join();
        _updateThread = null;
    }

    public void EnqueueGL(Action<GL> body)
        => _renderer?.Enqueue(new GlAction(body));

    public unsafe void EnqueueNative(Action<GL, Glfw, nint> body)
        => _renderer?.Enqueue(new NativeAction(body));


    /// <summary>
    /// Clears the window’s private offscreen buffer for this frame.
    /// </summary>
    /// <param name="color">Clear color</param>
    public void Clear(in Color? color = null) {
        if (color != null) ClearColor = color;
        _renderer?.Enqueue(new ClearAction(ClearColor));
    }

    /// <summary>
    /// Draws a user-defined shape (via <see cref="IDrawShape"/>) into this window for the current frame.
    /// </summary>
    /// <param name="shape">Shape that knows how to render itself with Skia onto a canvas.</param>
    public void Draw(in IDrawShape shape)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Composites a shared layer onto this window using an optional transform and material override.
    /// </summary>
    /// <param name="layer">Shared layer to sample.</param>
    /// <param name="transform">Optional placement transform (layer-local → window).</param>
    /// <param name="material">Optional material/shader override (engine-managed placeholder).</param>
    public void DrawLayer(CodeDrawLayer layer, Matrix3x3? transform = null, object? material = null)
        => throw new NotImplementedException();

    private long _lastTokenSubmitted = 0; // guarded by renderer methods

    /// <summary>Seal the current staging into a frame and return its token.
    /// If the previous frame hasn't presented yet, this call waits for it first (off render-thread).</summary>
    public long Show()
    {
        if (_renderer is null) return 0;

        // Auto-backpressure: don't let users enqueue faster than we can present.
        // Never block the render thread.
        if (!AbstractWindowRenderer.IsRenderThread(_renderer))
        {
            var pending = Interlocked.Read(ref _lastTokenSubmitted);  // todo: this is NOT a pending counter, its just a counter how many already where submitted...
            Console.WriteLine("pending: " + pending);
            if (pending != 0) _renderer.WaitForPresented(pending);
        } else
        {
            Console.WriteLine("Show on render thread!");
        }

        var t = _renderer.SealFrame();
        Interlocked.Exchange(ref _lastTokenSubmitted, t);
        return t;
    }

    /// <summary>Wait for a specific frame token (or the latest, if null) to be presented.</summary>
    public void WaitForRender(long? frameToken = null)
    {
        _renderer?.WaitForPresented(frameToken ?? Interlocked.Read(ref _lastTokenSubmitted));
    }


    /// <summary>
    /// Converts a point from window pixels to the local space of a target layer.
    /// </summary>
    /// <param name="layer">Target layer.</param>
    /// <param name="p">Point in window pixels.</param>
    /// <returns>Point in the layer's local space.</returns>
    public System.Numerics.Vector2 WindowToLayer(CodeDrawLayer layer, System.Numerics.Vector2 p)
        => throw new NotImplementedException();

    /// <summary>
    /// Converts a point from a layer's local space to window pixels.
    /// </summary>
    /// <param name="layer">Source layer.</param>
    /// <param name="p">Point in the layer's local space.</param>
    /// <returns>Point in window pixels.</returns>
    public System.Numerics.Vector2 LayerToWindow(CodeDrawLayer layer, System.Numerics.Vector2 p)
        => throw new NotImplementedException();
}
