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
/// <remarks>
/// Creates a new window object. The render thread and GL resources are not created
/// until <see cref="Open"/> is called.
/// </remarks>
/// <param name="title">Window title.</param>
public unsafe sealed partial class CodeDrawWindow(string title) : IDisposable
{
    private AbstractWindowRenderer? _renderer;
    private WindowHandle* _native;

    /// <summary>Title shown in the OS window chrome.</summary>
    public string Title { get; } = title ?? throw new ArgumentNullException(nameof(title));

    /// <summary>
    /// Drawable size in pixels. Safe to set before <see cref="Open"/>; marshalled to the UI thread after.
    /// </summary>
    public Vector2<int> Size { get; set; }

    /// <summary>Whether the window can be resized by the user.</summary>
    public bool Resizable { get; set; } = true;

    /// <summary>Whether vertical sync (vsync) is enabled.</summary>
    public bool VSync { get; set; } = false;

    /// <summary>Target frames per second. 0 = uncapped.</summary>
    public int TargetFPS { get; set; } = 60;

    /// <summary>Background color used to clear the default framebuffer each frame.</summary>
    public Color ClearColor { get; set; } = Color.BLACK;

    /// <summary>Monotonic time since this window started (after <see cref="Open"/>).</summary>
    public TimeSpan Uptime => _renderer?.Uptime ?? TimeSpan.Zero;

    /// <summary>Total number of frames presented by this window.</summary>
    public long Frames => _renderer?.Frames ?? 0;
    public double FPS => _renderer?.Fps ?? 0.0;

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

    private readonly RateMeter _updateUps = new(0.25);
    public double UPS => _updateUps.Ewma;

    private int _disposeGate; // 0 = not disposing, 1 = disposing/closed

    public bool IsClosed => _closedMre.IsSet;

    private readonly ManualResetEventSlim _loadedMre = new(initialState: false);
    internal void SignalLoadedComplete() => _loadedMre.Set();

    internal void RaiseLoaded(GL gl, Glfw glfw, WindowHandle* window) => Loaded?.Invoke(this, gl, glfw, (nint)window);

    /// <summary>
    /// Starts the render thread, creates the GL context, and begins the frame loop.
    /// Triggers <see cref="Loaded"/> once and then <see cref="BeforeRender"/> every frame.
    /// </summary>
    public void Open()
    {
        if (_closedMre.IsSet || Volatile.Read(ref _disposeGate) == 1)
            throw new InvalidOperationException("This window instance has been closed. Create a new CodeDrawWindow.");

        var host = CodeDrawHost.Instance;
        host.EnsureStarted();

        _native = host.CreateWindow(Size.X, Size.Y, Title);


        _renderer = new DefaultWindowRenderer(_native!, Title);
        _renderer.BindPublic(this);
        _renderer.Start();

        StartUpdateLoopIfNeeded();
        host.OnWindowCreated(_native!, this);

        _loadedMre.Wait();
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
            finally { _updateUps.Tick(); _updateUps.MaybeSample(); }

            if (UpdateIntervalMs > 0)
                Thread.Sleep(UpdateIntervalMs);
            else
                Thread.Yield();
        }
    }

    public void Dispose()
    {
        // Only the first caller runs teardown; others wait until closed.
        if (Interlocked.Exchange(ref _disposeGate, 1) == 1)
        {
            _closedMre.Wait();
            return;
        }

        try
        {
            // stop update loop
            _updateRunning = false;
            _updateThread?.Join();
            _updateThread = null;

            // ensure render thread is stopped
            _renderer?.StopAndJoin();
            _renderer = null;

            // unregister & destroy native window
            var host = CodeDrawHost.Instance;
            if (_native != null)
            {
                host.OnWindowDestroyed(_native);
                host.DestroyWindowAndMaybeStop(_native);
                _native = null;
            }

            try
            {
                Closed?.Invoke();
                CodeDrawEvents.RaiseClosed(this);
            }
            catch { /* ignore teardown-callback errors */ }
        }
        finally
        {
            _closedMre.Set(); // fully closed
        }
    }

    public void EnqueueGL(Action<GL> body)
        => _renderer?.Enqueue(new GlAction(body));

    public unsafe void EnqueueNative(Action<GL, Glfw, nint> body)
        => _renderer?.Enqueue(new NativeAction(body));


    /// <summary>
    /// Clears the window’s private offscreen buffer for this frame.
    /// </summary>
    /// <param name="color">Clear color</param>
    public void Clear(in Color? color = null, ClearMask mask = ClearMask.Color) {
        if (color is not null) ClearColor = color;
        _renderer?.Enqueue(new ClearAction(ClearColor, mask));
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
            if (pending != 0) _renderer.WaitForPresented(pending);
        } else
            Console.WriteLine("Show on render thread!");

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
