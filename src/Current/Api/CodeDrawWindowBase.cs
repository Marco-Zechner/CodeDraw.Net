using System.Diagnostics;
using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using MarcoZechner.CodeDrawDotNet.Engine.Implementations;
using MarcoZechner.DiagnosticsDotNet;
using MarcoZechner.CodeDrawDotNet.Api.Events;
using MarcoZechner.CodeDrawDotNet.Engine.Implementations.Actions;
using MarcoZechner.CodeDrawDotNet.Api.Graphics;

namespace MarcoZechner.CodeDrawDotNet.Api;

public abstract unsafe partial class CodeDrawWindowBase(string title) : IDisposable
{
    // 1) Fields
    private AbstractWindowRenderer? _renderer;
    protected WindowHandle* _native;

    private Thread? _updateThread;
    private volatile bool _updateRunning;
    private readonly ManualResetEventSlim _loadedMre = new(initialState: false);
    private readonly ManualResetEventSlim _closedMre = new(initialState: false);
    private int _disposeGate;

    private long _lastTokenSubmitted = 0;
    private readonly RateMeter _updateUps = new(0.25);
    private CloseReason _closeReason = CloseReason.Unknown;

    // 2) Properties (public API)
    public string Title { get; } = title ?? throw new ArgumentNullException(nameof(title));
    public Vector2<int> Size { get; set; }
    public bool Resizable { get; set; } = true;
    public bool VSync { get; set; } = false;
    public int TargetFPS { get; set; } = 60;
    public Color ClearColor { get; set; } = Color.BLACK;
    public TimeSpan Uptime => _renderer?.Uptime ?? TimeSpan.Zero;
    public long Frames => _renderer?.Frames ?? 0;
    public double FPS => _renderer?.Fps ?? 0.0;
    public object? Tag { get; set; }
    public double UPS => _updateUps.Ewma;
    public bool IsClosed => _closedMre.IsSet;
    /// <summary>
    /// Number of frames currently queued or in-flight (backlog).
    /// </summary>
    public int BacklogFrames => _renderer?.BacklogFrames ?? 0;

    /// <summary>
    /// Number of frames currently queued but not yet rendered.
    /// </summary>
    public int QueuedFrames => _renderer?.QueuedFrames ?? 0;

    /// <summary>
    /// Number of frames currently being processed by the renderer (in-flight only).
    /// </summary>
    public int InflightFrames => _renderer?.InflightFrames ?? 0;

    // 3) Events
    public event Action<CodeDrawWindowBase, GL, Glfw, nint>? Loaded;
    public event Action<CodeDrawWindowBase, double>? Update;
    public event CloseRequestedHandler? CloseRequested;
    public event Action? Closed;

    // Tunables
    public int UpdateIntervalMs { get; set; } = 10;
    public int LongActionWarnMs { get; set; } = 16;
    public int MaxOutstandingFrames
    {
        get => _renderer?.MaxInflightFrames ?? 3;
        set { if (_renderer is not null) _renderer.MaxInflightFrames = value; }
    }

    // 5) Lifecycle
    public void Open()
    {
        if (_closedMre.IsSet || Volatile.Read(ref _disposeGate) == 1)
            throw new InvalidOperationException("This window instance has been closed. Create a new window.");

        var host = CodeDrawHost.Instance;
        host.EnsureStarted();

        _native = host.CreateWindow(Size.X, Size.Y, Title);

        _renderer = CreateRenderer(_native, Title);
        _renderer.BindPublic(this);
        _renderer.Start();

        StartUpdateLoopIfNeeded();
        host.OnWindowCreated(_native!, this);

        // Wait until both per-window and global Loaded handlers have completed
        _loadedMre.Wait();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeGate, 1) == 1)
        {
            _closedMre.Wait();
            return;
        }

        try
        {
            _updateRunning = false;
            _updateThread?.Join();
            _updateThread = null;

            _renderer?.StopAndJoin();
            _renderer = null;

            var host = CodeDrawHost.Instance;
            if (_native != null)
            {
                host.OnWindowDestroyed(_native);
                host.DestroyWindowAndMaybeStop(_native);
                _native = null;
            }

            try { Closed?.Invoke(); } catch { }
            try { CodeDrawEvents.RaiseClosed(this); } catch { }
        }
        finally
        {
            _closedMre.Set();
            GC.SuppressFinalize(this);
        }
    }

    // 6) Drawing & presentation
    public void EnqueueGL(Action<GL> body) => _renderer?.Enqueue(new GlAction(body));
    public void EnqueueNative(Action<GL, Glfw, nint> body) => _renderer?.Enqueue(new NativeAction(body));

    public void Clear(in Color? color = null, ClearMask mask = ClearMask.Color)
    {
        if (color is not null) ClearColor = color;
        _renderer?.Enqueue(new ClearAction(ClearColor, mask));
    }

    public long Show()
    {
        if (_renderer is null) return 0;

        // Never block the render thread itself
        if (!AbstractWindowRenderer.IsRenderThread(_renderer))
        {
            // bounded back-pressure: wait until in-flight < MaxInflightFrames
            _renderer.WaitForInflightSlot();
        }
        else
        {
            Console.WriteLine("[WARN] Show() called from render thread; skipping inflight wait to avoid deadlock.");
        }

        var t = _renderer.SealFrame();
        Interlocked.Exchange(ref _lastTokenSubmitted, t);
        return t;
    }

    public void WaitForRender(long? frameToken = null)
    {
        _renderer?.WaitForPresented(frameToken ?? Interlocked.Read(ref _lastTokenSubmitted));
    }

    // 7) Requests

    // --- request Close overload that posts to UI thread (no direct callback) ---
    public void Close()
    {
        RequestClose(CloseReason.RequestedByUser);
    }

    /// <summary>
    /// Wait until close; if the user presses <paramref name="triggerKey"/> in the console,
    /// this will call <see cref="RequestClose"/> (same as clicking X).
    /// </summary>
    public CloseReason WaitForClose(ConsoleKey triggerKey) => WaitForClose(k => k.Key == triggerKey);


    public CloseReason WaitForClose(Func<ConsoleKeyInfo, bool>? shouldCloseOnKey = null)
    {
        if (IsClosed) return CloseReason.AlreadyClosed;
        _closeReason = CloseReason.Unknown;

        if (shouldCloseOnKey is null)
        {
            _closedMre.Wait();
            return _closeReason;
        }
        while (!_closedMre.Wait(10))
        {
            if (Console.KeyAvailable)
            {
                var k = Console.ReadKey(intercept: true);
                try { if (shouldCloseOnKey(k)) RequestClose(CloseReason.WaitForCloseEvent); }
                catch { /* ignore user callback exceptions */ }

            }
        }
        return _closeReason;
    }

    // 8) Internal hooks used by renderer / host
    internal void RequestClose(CloseReason reason)
    {
        if (_native == null) return;
        var host = CodeDrawHost.Instance;
        host.EnqueueUI(() =>
        {
            host.Glfw.SetWindowShouldClose(_native, true);
            OnNativeCloseRequestedFromUI(reason);
        });
    }

    internal void SignalLoadedComplete() => _loadedMre.Set();

    internal void RaiseLoaded(GL gl, Glfw glfw, WindowHandle* window)
        => Loaded?.Invoke(this, gl, glfw, (nint)window);

    internal void RaiseCloseRequested(CloseEventArgs args, CloseReason reason)
        => CloseRequested?.Invoke(this, args, reason);

    internal void NotifyRendererStopped() => _closedMre.Set();


    // --- UI-thread close entry from GlfwCallbackHub ---
    internal unsafe void OnNativeCloseRequestedFromUI(CloseReason reason = CloseReason.UserClosedWindow)
    {
        var args = new CloseEventArgs();

        // 1) per-window first
        try { CloseRequested?.Invoke(this, args, reason); }
        catch { /* swallow to not break UI loop */ }

        // 2) global mirror second
        try { CodeDrawEvents.RaiseCloseRequested(this, args, reason); }
        catch { /* swallow to not break UI loop */ }

        var host = CodeDrawHost.Instance;

        if (args.Cancel)
        {
            // veto — clear GLFW flag and continue
            host.Glfw.SetWindowShouldClose(_native, false);
            return;
        }

        // proceed with teardown off the UI thread to avoid stalling event dispatch
        _closeReason = reason;
        ThreadPool.QueueUserWorkItem(_ => Dispose());
    }

    // 9) Protected / abstract
    protected abstract AbstractWindowRenderer CreateRenderer(WindowHandle* native, string title);

    // 10) Private helpers
    private void StartUpdateLoopIfNeeded()
    {
        if (UpdateIntervalMs <= 0 || Update is null) return;

        _updateRunning = true;
        _updateThread = new Thread(UpdateLoop) { IsBackground = true, Name = $"Update-{Title}" };
        _updateThread.Start();
    }

    private void UpdateLoop()
    {
        var sw = Stopwatch.StartNew();
        double last = 0;

        while (_updateRunning)
        {
            double now = sw.Elapsed.TotalSeconds;
            double dt = now - last; last = now;

            try { Update?.Invoke(this, dt); }
            catch (Exception ex) { Console.WriteLine($"[Update ERROR] {ex}"); }
            finally { _updateUps.Tick(); _updateUps.MaybeSample(); }

            if (UpdateIntervalMs > 0) Thread.Sleep(UpdateIntervalMs);
            else Thread.Yield();
        }
    }
}
