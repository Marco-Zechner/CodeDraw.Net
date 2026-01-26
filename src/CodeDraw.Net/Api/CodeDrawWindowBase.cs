using System.Diagnostics;
using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using MarcoZechner.DiagnosticsDotNet;
using MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;
using MarcoZechner.CodeDrawDotNet.Api.Graphics.Enums;
using MarcoZechner.CodeDrawDotNet.Engine;
using MarcoZechner.CodeDrawDotNet.Interfaces;
using MarcoZechner.CodeDrawDotNet.Interfaces.Primitives;

namespace MarcoZechner.CodeDrawDotNet.Api;

public abstract unsafe partial class CodeDrawWindowBase(string title) : IDisposable, IRenderThreadCallbacks, IWindowSettings
{
    // 1) Fields
    protected IWindowHost Host => CodeDrawRuntime.Host;
    private IAttachableRenderer? _renderer;
    // 9) Protected / abstract
    protected abstract IAttachableRenderer CreateRenderer();
    protected WindowHandle* Native;

    private Thread? _updateThread;
    private volatile bool _updateRunning;
    private readonly ManualResetEventSlim _loadedMre = new(initialState: false);
    private readonly ManualResetEventSlim _closedMre = new(initialState: false);
    private int _disposeGate;

    private long _lastTokenSubmitted;
    private readonly RateMeter _updateUps = new(0.25);
    private CloseReason _closeReason = CloseReason.UNKNOWN;
    private BlendMode2D _blendMode2D = BlendMode2D.RGB_BLEND_KEEP_DST_ALPHA;

    // 2) Properties (public API)
    public string Title { get; } = title ?? throw new ArgumentNullException(nameof(title));
    private Vector2<int> _size = new(1280,720);

    public Vector2<int> Size
    {
        get => _size;
        set
        {
            _size = value;
            if (Native != null)
                Host.ResizeWindow(Native, _size.X, _size.Y);
        }
    }
    public bool Resizable { get; set; } = true;
    public bool VSync { get; set; }
    public int TargetFps { get; set; } = 60;
    public Color ClearColor { get; set; } = Color.Black;
    public TimeSpan Uptime => _renderer?.Uptime ?? TimeSpan.Zero;
    public long Frames => _renderer?.Frames ?? 0;
    public double Fps => _renderer?.Fps ?? 0.0;
    public double Ups => _updateUps.Ewma;
    public bool IsClosed => _closedMre.IsSet;
    public bool IsOpen => _loadedMre.IsSet;
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

    public ILayerHandle? CanvasLayer => (_renderer as Renderers.Default.DefaultWindowRenderer)?.CanvasLayer;


    // 3) Events
    public event Action<CodeDrawWindowBase, GL, Glfw, nint>? Loaded;
    public event Action<CodeDrawWindowBase, double>? Update;
    public event CloseRequestedHandler? CloseRequested;
    public event Action? Closed;

    // Tunables
    public int UpdateIntervalMs { get; set; } = 10;
    public int LongActionWarnMs { get; set; } = 16;
    public int MaxInflightFrames
    {
        get => _renderer?.MaxInflightFrames ?? 3;
        set { if (_renderer is not null) _renderer.MaxInflightFrames = value; else _lastSetMaxInflightFrames = value; }
    }
    private int _lastSetMaxInflightFrames = 3;

    // 5) Lifecycle
    public void Open()
    {
        if (_closedMre.IsSet || Volatile.Read(ref _disposeGate) == 1)
            throw new InvalidOperationException("This window instance has been closed. Create a new window.");

        _renderer = CreateRenderer();
        if (_renderer is Engine.AbstractWindowRenderer awr)
            awr.SetBlendModeForFrameSync(_blendMode2D);

        var host = Host;
        host.EnsureStarted();

        Native = host.CreateWindow(Size.X, Size.Y, Title);

        host.OnWindowCreated(Native!, this);

        _renderer.Attach(host, (nint)Native, Title, this, this);
        _renderer.MaxInflightFrames = _lastSetMaxInflightFrames;
        _renderer.Start();

        StartUpdateLoopIfNeeded();

        // Wait until both per-window and global Loaded handlers have completed

        //TODO set internal size here? and switch to polling the size from the window i guess


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

            var host = CodeDrawRuntime.Host;
            if (Native != null)
            {
                host.OnWindowDestroyed(Native);
                host.DestroyWindowAndMaybeStop(Native);
                Native = null;
            }

            try { Closed?.Invoke(); }
            catch
            {
                // ignored
            }

            try { CodeDrawEvents.RaiseClosed(this); }
            catch
            {
                // ignored
            }
        }
        finally
        {
            _loadedMre.Reset();
            _closedMre.Set();
            GC.SuppressFinalize(this);
        }
    }

    // 6) Drawing & presentation
    public void EnqueueGl(Action<GL> body) => _renderer?.Enqueue(new GlAction(body));
    public void EnqueueNative(Action<GL, Glfw, nint> body) => _renderer?.Enqueue(new NativeAction(body));

    public void Clear(in Color? color = null, ClearMask mask = ClearMask.COLOR)
    {
        if (color is not null) ClearColor = color;
        _renderer?.Enqueue(new ClearAction(ClearColor, mask, _blendMode2D));
    }

    public long Show()
    {
        if (_renderer is null) return 0;

        // Never block the render thread itself
        if (_renderer.IsRenderThread())
        {
            Console.WriteLine("[ERROR] Show() called from render thread; ignoring.");
            return 0;
        }

        // bounded back-pressure: wait until in-flight < MaxInflightFrames
        _renderer.WaitForInflightSlot();
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
        RequestClose(CloseReason.REQUESTED_BY_USER);
    }

    /// <summary>
    /// Wait until close; if the user presses <paramref name="triggerKey"/> in the console,
    /// this will call <see cref="RequestClose"/> (same as clicking X).
    /// </summary>
    public CloseReason WaitForClose(ConsoleKey triggerKey) => WaitForClose(k => k.Key == triggerKey);


    public CloseReason WaitForClose(Func<ConsoleKeyInfo, bool>? shouldCloseOnKey = null)
    {
        if (IsClosed) return CloseReason.ALREADY_CLOSED;
        _closeReason = CloseReason.UNKNOWN;

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
                try { if (shouldCloseOnKey(k)) RequestClose(CloseReason.WAIT_FOR_CLOSE_EVENT); }
                catch { /* ignore user callback exceptions */ }

            }
        }
        return _closeReason;
    }

    // 8) Internal hooks used by renderer / host
    internal void RequestClose(CloseReason reason)
    {
        if (Native == null) return;
        CodeDrawRuntime.Host.RequestClose(Native, reason);
    }

    internal void RaiseCloseRequested(CloseEventArgs args, CloseReason reason)
        => CloseRequested?.Invoke(this, args, reason);

    internal void NotifyRendererStopped() => _closedMre.Set();

    internal void OnFramebufferSizeFromUi(int fbW, int fbH)
    {
        if (_renderer is AbstractWindowRenderer awr)
            awr.SetFramebufferSizeFromUi(fbW, fbH);
    }

    public void OnResizeInProgressFromUi(bool v) //TODO make internal
    {
        if (_renderer is AbstractWindowRenderer awr)
            awr.SetResizeInProgressFromUi(v);
    }

    // --- UI-thread close entry from GlfwCallbackHub ---
    internal void OnNativeCloseRequestedFromUI(CloseReason reason = CloseReason.USER_CLOSED_WINDOW)
    {
        var args = new CloseEventArgs();

        // 1) per-window first
        try { CloseRequested?.Invoke(this, args, reason); }
        catch { /* swallow to not break UI loop */ }

        // 2) global mirror second
        try { CodeDrawEvents.RaiseCloseRequested(this, args, reason); }
        catch { /* swallow to not break UI loop */ }

        var host = CodeDrawRuntime.Host;

        if (args.Cancel)
        {
            // veto — clear GLFW flag and continue
            host.GlfwUnsafe.SetWindowShouldClose(Native, false);
            return;
        }

        // proceed with teardown off the UI thread to avoid stalling event dispatch
        _closeReason = reason;
        ThreadPool.QueueUserWorkItem(_ => Dispose());
    }

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
            double dt = now - last;
            last = now;

            try { Update?.Invoke(this, dt); }
            catch (Exception ex) { Console.WriteLine($"[Update ERROR] {ex}"); }
            finally { _updateUps.Tick(); _updateUps.MaybeSample(); }

            if (UpdateIntervalMs - dt * 1000 > 0) Thread.Sleep((int)(UpdateIntervalMs - dt * 1000));
            else Thread.Yield();
        }
    }

    public void OnLoaded(GL gl, Glfw glfw, nint window)
    {
        SetBlendMode2D(_blendMode2D);

        Loaded?.Invoke(this, gl, glfw, window);
        CodeDrawEvents.RaiseLoaded(this, gl, glfw, (WindowHandle*)window); //TODO cast?
        _loadedMre.Set();
    }

    public void SetBlendMode2D(BlendMode2D mode)
    {
        _blendMode2D = mode;

        if (_renderer is Engine.AbstractWindowRenderer awr)
            awr.SetBlendModeForFrameSync(mode);

        _renderer?.Enqueue(new SetBlendMode2DAction(mode));
    }

    public void SetWindowAlpha(float a)
    {
        var prev = _blendMode2D;
        SetBlendMode2D(BlendMode2D.WRITE_ALPHA_REPLACE);
        FillRect(0, 0, Size.X, Size.Y, new Color(0,0,0,a));
        SetBlendMode2D(prev);
    }

    public void OnPresented(long token)
    {
        //TODO ? something
    }
}
