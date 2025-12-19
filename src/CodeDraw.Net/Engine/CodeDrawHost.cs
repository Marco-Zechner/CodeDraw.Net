using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MarcoZechner.CodeDrawDotNet.Interfaces;
using MarcoZechner.DiagnosticsDotNet;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Engine;

internal unsafe sealed class CodeDrawHost : IDisposable, IWindowHost
{
    public static CodeDrawHost Instance { get; } = new();

    public Glfw Glfw => _glfw!;
    public WindowHandle* ShareRoot => _shareRoot;

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
        try { _glfw?.PostEmptyEvent(); } catch { }
        _uiThread?.Join();
        _uiThread = null;
    }

    /// <summary>Enqueue a job to be executed on the UI/GLFW thread (fire-and-forget).
    /// <br></br> NEVER render stuff here, GLFW.PollEvents will block this loop while dragging or resizing a window. (on windows)
    /// </summary>
    public void EnqueueUi(Action job)
    {
        _uiJobs.Enqueue(job);
        try { _glfw?.PostEmptyEvent(); } catch { }
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
            ApplyCommonHints(_glfw!);
            result = _glfw!.CreateWindow(w, h, title, null, _shareRoot);
            if (result == null) throw new Exception("CreateWindow failed");

            // Bind once for stability, then unbind (esp. on Windows)
            _glfw.MakeContextCurrent(result);
            _glfw.MakeContextCurrent(null);

            IncWindowRef();
        });
        return result!;
    }

    /// <summary>Request destruction of a window on the UI thread.</summary>
    public void DestroyWindowAndMaybeStop(WindowHandle* win)
    {
        if (win == null) { TryStopIfUnused(); return; }

        // Destroy on UI thread, then adjust counts on the caller thread so we can call Stop() safely
        EnqueueUiSync(() =>
        {
            _glfw!.MakeContextCurrent(null);
            _glfw.DestroyWindow(win);
        });

        DecWindowRef();
        TryStopIfUnused();
    }

    private void UiThreadMain()
    {
        _glfw = Glfw.GetApi();
        if (!_glfw.Init()) throw new Exception("GLFW init failed");

        // Shared hidden root
        ApplyCommonHints(_glfw);
        _shareRoot = _glfw.CreateWindow(1, 1, "share-root", null, null);
        if (_shareRoot == null) throw new Exception("Failed to create share-root");
        _glfw.HideWindow(_shareRoot);
        // _glfw.MakeContextCurrent(_shareRoot); //TODO removed if not needed
        AttachCallbacksFor(_shareRoot); // so global metrics also see root events

        // Create a GL instance once here (optional). We don’t keep it; render threads get their own.
        // var gl = GL.GetApi(_glfw.GetProcAddress); //TODO removed if not needed
        // _glfw.MakeContextCurrent(null); //TODO removed if not needed

        StartTimeUtc = DateTime.UtcNow;
        _started.Set();

        while (_running)
        {
            // 1) Drain UI jobs on UI thread; time & count them
            while (_uiJobs.TryDequeue(out var j))
            {
                try { using (_busy.Scope()) j(); }
                catch (Exception ex) { Console.WriteLine($"[UI job error] {ex}"); }
                finally { _work.OnJob(); }
            }

            _busy.MaybeSample();
            _work.MaybeSample();

            // 2) Block until OS or PostEmptyEvent wakes us (dispatches events internally)
            _glfw.WaitEvents();

            // Coarse accounting: at least one batch occurred
            _work.OnJob();
        }

        // Drain pending jobs (e.g., destroys)
        while (_uiJobs.TryDequeue(out var j2))
        {
            try { j2(); } catch (Exception ex) { Console.WriteLine($"[UI drain error] {ex}"); }
        }

        DetachCallbacksFor(_shareRoot);

        // Cleanup share root & terminate
        if (_shareRoot != null)
        {
            _glfw.MakeContextCurrent(null);
            _glfw.DestroyWindow(_shareRoot);
            _shareRoot = null;
        }
        _glfw.Terminate();
        _glfw = null;
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

    unsafe void IWindowHost.OnWindowCreated(WindowHandle* win, IWindowEventSink sink)
    {
        OnWindowCreated(win, sink);
    }

    unsafe void IWindowHost.OnWindowDestroyed(WindowHandle* win)
    {
        OnWindowDestroyed(win);
    }

    public unsafe void SetWindowShouldClose(WindowHandle* win, bool shouldClose)
    {
        EnqueueUi(() =>
        {
            Glfw.SetWindowShouldClose(win, true);
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
                Glfw.SetWindowShouldClose(winPtr, true);
                sink.OnNativeCloseRequestedFromUI(CloseReason.REQUESTED_BY_USER);
            }
        });
    }
}
