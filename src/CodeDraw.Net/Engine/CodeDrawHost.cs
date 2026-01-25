using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MarcoZechner.CodeDrawDotNet.Interfaces;
using MarcoZechner.CodeDrawDotNet.Interfaces.Primitives;
using MarcoZechner.DiagnosticsDotNet;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Engine;

internal sealed unsafe class CodeDrawHost : IDisposable, IWindowHost
{
    public static CodeDrawHost Instance { get; } = new();

    [Obsolete("Do not access GLFW directly. Use WithGlfw / EnqueueUi instead.", error: true)]
    public Glfw Glfw => _glfw!;
    public WindowHandle* ShareRoot => _shareRoot;
    private readonly AutoResetEvent _uiWake = new(false);

    private readonly ConcurrentDictionary<nint, IWindowEventSink> _winMap = new();
    internal void RegisterWindow(WindowHandle* h, IWindowEventSink w) => _winMap[(nint)h] = w;
    internal void UnregisterWindow(WindowHandle* h) => _winMap.TryRemove((nint)h, out _);
    internal IWindowEventSink? ResolveWindow(WindowHandle* h)
        => _winMap.TryGetValue((nint)h, out var w) ? w : null;

    public DateTime StartTimeUtc { get; private set; }

    private Thread? _uiThread;
    private Glfw? _glfw;
    private volatile bool _running;
    private readonly AutoResetEvent _started = new(false);
    private readonly ConcurrentQueue<Action> _uiJobs = new();

    private WindowHandle* _shareRoot = null;

    private LayerWorker? _layerWorker;
    public LayerWorker Layers => _layerWorker!; //TODO null?

    private int _activeWindows = 0;
    private int _activeLayers  = 0; // reserve for future layers

    public int ActiveWindows => Volatile.Read(ref _activeWindows);
    public int ActiveLayers  => Volatile.Read(ref _activeLayers);

    private void IncWindowRef() => Interlocked.Increment(ref _activeWindows);
    private void DecWindowRef() => Interlocked.Decrement(ref _activeWindows);

    internal void IncLayerRef() => Interlocked.Increment(ref _activeLayers);
    internal void DecLayerRef() => Interlocked.Decrement(ref _activeLayers);

    private readonly BusyMeter _busy = new(0.25);
    private readonly WorkRate  _work = new();

    public double HostBusyPercent => _busy.Duty * 100.0;

    public double HostJobsPerSec  => _work.JobsPerSec;
    public double HostIdleSec     => _work.IdleSeconds;

    public ILayerMetricsProvider LayerMetrics => _layerWorker!; //TODO null?

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IDisposable BeginGlfwEventScope() => _busy.Scope();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void OnGlfwEvent() => _work.OnJob();

    private readonly ConcurrentDictionary<nint, GlfwCallbackHub> _hubs = new();

    private void AttachCallbacksFor(WindowHandle* win)
        => _hubs[(nint)win] = new GlfwCallbackHub(_glfw!, win, this);

    private void DetachCallbacksFor(WindowHandle* win)
    {
        if (_hubs.TryRemove((nint)win, out var hub))
            hub.Uninstall();
    }


    private void TryStopIfUnused()
    {
        if (ActiveWindows == 0 && ActiveLayers == 0)
            Close();
    }

    public void EnsureStarted()
    {
        if (_running) return;
        _running = true;
        _uiThread = new Thread(UiThreadMain) { IsBackground = true, Name = "CodeDraw-GLFW-UI" };
        _uiThread.Start();
        _started.WaitOne(); // wait until GLFW + share root created

        _layerWorker = new LayerWorker(_glfw!);
        _layerWorker.Start();
    }

    public void Dispose() => Close();

    private void Close()
    {
        if (!_running) return;

        _layerWorker?.Stop();
        _layerWorker = null;

        _running = false;
        _uiWake.Set(); // wake UI loop so it can exit

        try { WithGlfw(glfw => glfw.PostEmptyEvent()); } catch { }

        _uiThread?.Join();
        _uiThread = null;
    }

    /// <summary>Enqueue a job to be executed on the UI/GLFW thread (fire-and-forget).
    /// <br></br> NEVER render stuff here, GLFW.PollEvents will block this loop while dragging or resizing a window. (on windows)
    /// </summary>
    public void EnqueueUi(Action job)
    {
        _uiJobs.Enqueue(job);

        // Wake UI loop (no GLFW needed)
        _uiWake.Set();

        // Optional: keep PostEmptyEvent as "extra nudge", but DO NOT depend on it.
        try { WithGlfw(glfw => glfw.PostEmptyEvent()); } catch { }
    }

    /// <summary>Execute a job on the UI/GLFW thread and wait for completion.
    /// <br></br> NEVER render stuff here, GLFW.PollEvents will block this loop while dragging or resizing a window. (on windows)
    /// </summary>
    public void EnqueueUiSync(Action job)
    {
        var done = new AutoResetEvent(false);
        Exception? ex = null;

        EnqueueUi(() =>
        {
            try { job(); }
            catch (Exception e) { ex = e; }
            finally { done.Set(); }
        });

        done.WaitOne();
        if (ex != null) throw ex;
    }

    /// <summary>Create a visible window in the share group (runs on UI thread).</summary>
    public WindowHandle* CreateWindow(int w, int h, string title)
    {
        WindowHandle* result = null;
        EnqueueUiSync(() =>
        {
            WithGlfw(glfw =>
            {
                ApplyCommonHints(glfw);
                result = glfw.CreateWindow(w, h, title, null, _shareRoot);
                if (result == null) throw new Exception("CreateWindow failed");

                glfw.MakeContextCurrent(result);
                glfw.MakeContextCurrent(null);
            });

            IncWindowRef();
        });
        return result!;
    }

    /// <summary>Request destruction of a window on the UI thread.</summary>
    public void DestroyWindowAndMaybeStop(WindowHandle* win)
    {
        if (win == null) { TryStopIfUnused(); return; }

        EnqueueUiSync(() =>
        {
            WithGlfw(glfw =>
            {
                glfw.MakeContextCurrent(null);
                glfw.DestroyWindow(win);
            });
        });

        DecWindowRef();
        TryStopIfUnused();
    }

    private readonly object _glfwLock = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T WithGlfw<T>(Func<Glfw, T> f)
    {
        lock (_glfwLock) return f(_glfw!);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WithGlfw(Action<Glfw> f)
    {
        lock (_glfwLock) f(_glfw!);
    }

    private void UiThreadMain()
    {
        _glfw = Glfw.GetApi();
        if (!_glfw.Init()) throw new Exception("GLFW init failed");

        // NOTE: set callbacks under lock as well (consistency)
        lock (_glfwLock)
        {
            _glfw.SetErrorCallback((error, description) =>
            {
                Console.WriteLine($"GLFW Error: {error} - {description}");
            });

            ApplyCommonHints(_glfw);

            _shareRoot = _glfw.CreateWindow(1, 1, "share-root", null, null);
            if (_shareRoot == null) throw new Exception("Failed to create share-root");
            _glfw.HideWindow(_shareRoot);
        }

        AttachCallbacksFor(_shareRoot);

        StartTimeUtc = DateTime.UtcNow;
        _started.Set();

        while (_running)
        {
            // 1) Drain UI jobs
            while (_uiJobs.TryDequeue(out var j))
            {
                try { using (_busy.Scope()) j(); }
                catch (Exception ex) { Console.WriteLine($"[UI job error] {ex}"); }
                finally { _work.OnJob(); }
            }

            _busy.MaybeSample();
            _work.MaybeSample();

            // 2) Pump GLFW events QUICKLY under lock (non-blocking)
            WithGlfw(glfw => glfw.PollEvents());

            // 3) Sleep until we have work or after a short timeout
            // timeout keeps input responsive even if nobody posts events/jobs
            _uiWake.WaitOne(8);

            _work.OnJob();
        }

        // Drain pending jobs
        while (_uiJobs.TryDequeue(out var j2))
        {
            try { j2(); } catch (Exception ex) { Console.WriteLine($"[UI drain error] {ex}"); }
        }

        DetachCallbacksFor(_shareRoot);

        lock (_glfwLock)
        {
            if (_shareRoot != null)
            {
                _glfw.MakeContextCurrent(null);
                _glfw.DestroyWindow(_shareRoot);
                _shareRoot = null;
            }
            _glfw.Terminate();
            _glfw = null;
        }
    }

    private static void ApplyCommonHints(Glfw glfw)
    {
        // GL 3.3 core
        glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

        // Pixel format consistent across share group
        glfw.WindowHint(WindowHintInt.RedBits, 8);
        glfw.WindowHint(WindowHintInt.GreenBits, 8);
        glfw.WindowHint(WindowHintInt.BlueBits, 8);
        glfw.WindowHint(WindowHintInt.AlphaBits, 8);
        glfw.WindowHint(WindowHintInt.DepthBits, 24);
        glfw.WindowHint(WindowHintInt.StencilBits, 8);

        // QoL
        glfw.WindowHint(WindowHintBool.Resizable, true);
        glfw.WindowHint(WindowHintBool.Decorated, true);
        glfw.WindowHint(WindowHintBool.DoubleBuffer, true);
        glfw.WindowHint(WindowHintBool.TransparentFramebuffer, true);
    }

    internal void OnWindowCreated(WindowHandle* win, IWindowEventSink w)
    {
        // register mapping & install callbacks
        RegisterWindow(win, w);
        AttachCallbacksFor(win);
    }
    internal void OnWindowDestroyed(WindowHandle* win)
    {
        DetachCallbacksFor(win);
        UnregisterWindow(win);
    }

    void IWindowHost.OnWindowCreated(WindowHandle* win, IWindowEventSink sink)
    {
        OnWindowCreated(win, sink);
    }

    void IWindowHost.OnWindowDestroyed(WindowHandle* win)
    {
        OnWindowDestroyed(win);
    }

    public void SetWindowShouldClose(WindowHandle* win, bool shouldClose)
    {
        EnqueueUi(() =>
        {
            WithGlfw(glfw => glfw.SetWindowShouldClose(win, shouldClose));
        });
    }

    public void CloseAllWindows()
    {
        EnqueueUiSync(() =>
        {
            foreach (var kvp in _winMap)
            {
                var winPtr = (WindowHandle*)kvp.Key;
                var sink  = kvp.Value;
                WithGlfw(glfw => glfw.SetWindowShouldClose(winPtr, true));
                sink.OnNativeCloseRequestedFromUI(CloseReason.REQUESTED_BY_USER);
            }
        });
    }

    public void RequestClose(WindowHandle* win, CloseReason reason)
    {
        EnqueueUi(() =>
        {
            WithGlfw(glfw => glfw.SetWindowShouldClose(win, true));
            ResolveWindow(win)?.OnNativeCloseRequestedFromUI(reason);
        });
    }

    public void ResizeWindow(WindowHandle* win, int width, int height)
    {
        EnqueueUi(() =>
        {
            WithGlfw(glfw => glfw.SetWindowSize(win, width, height));
        });
    }
}
