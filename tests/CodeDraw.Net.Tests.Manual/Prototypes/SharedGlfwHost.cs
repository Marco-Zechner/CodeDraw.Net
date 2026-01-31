using System.Collections.Concurrent;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe class SharedGlfwHost : IDisposable
{
    public readonly record struct MouseMoveEvent(int WindowId, double X, double Y);
    public readonly record struct MouseButtonEvent(int WindowId, MouseButton Button, InputAction Action, KeyModifiers Mods);
    public readonly record struct MouseWheelEvent(int WindowId, double Dx, double Dy);
    public readonly record struct KeyEvent(int WindowId, Keys Key, int Scancode, InputAction Action, KeyModifiers Mods);
    public readonly record struct CharEvent(int WindowId, uint Codepoint);

    public readonly record struct MonitorInfo(
        nint GlfwHandle,
        string Name,
        int X, int Y,
        int Width, int Height,
        float ContentScaleX,
        float ContentScaleY,
        int RefreshRate
    );

    public readonly record struct WindowPlacement(
        int X, int Y,
        int Width, int Height,
        bool BorderlessFullscreen,
        int MonitorIndex
    );

    public static SharedGlfwHost Instance { get; } = new();

    public Glfw Glfw => _glfw!;
    public WindowHandle* ShareRoot => _shareRoot;

    private Thread? _uiThread;
    private Glfw? _glfw;
    private volatile bool _running;
    private readonly AutoResetEvent _started = new(false);
    private readonly AutoResetEvent _work = new(false);
    private readonly ConcurrentQueue<Action> _uiJobs = new();

    private WindowHandle* _shareRoot = null;

    private int _nextWindowId;
    private readonly ConcurrentDictionary<nint, int> _winToId = new();

    // NEW: per-window input queues (windowId -> queue)
    private readonly ConcurrentDictionary<int, ConcurrentQueue<object>> _inputQueues = new();

    private SharedGlfwHost() { }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _uiThread = new Thread(UiThreadMain) { IsBackground = true, Name = "GLFW-UI" };
        _uiThread.Start();
        _started.WaitOne();
    }

    public void Dispose() => Stop();

    public void Stop()
    {
        if (!_running) return;
        EnqueueUi(() => _running = false);
        _uiThread?.Join();
        _uiThread = null;
    }

    public void EnqueueUi(Action job)
    {
        _uiJobs.Enqueue(job);
        _work.Set();
    }

    internal void DrainWindowInput(int windowId, Action<object> handle, int max = 50_000)
    {
        if (!_inputQueues.TryGetValue(windowId, out var q)) return;

        var n = 0;
        while (n++ < max && q.TryDequeue(out var evt))
            handle(evt);
    }

    internal int GetWindowId(WindowHandle* win)
    {
        if (win == null) return 0;
        return _winToId.TryGetValue((nint)win, out var id) ? id : 0;
    }

    public WindowHandle* CreateWindow(int w, int h, string title)
    {
        WindowHandle* result = null;
        using var done = new AutoResetEvent(false);

        EnqueueUi(() =>
        {
            ApplyCommonHints(_glfw!);
            result = _glfw!.CreateWindow(w, h, title, null, _shareRoot);
            if (result == null) throw new Exception("CreateWindow failed");

            var id = Interlocked.Increment(ref _nextWindowId);
            _winToId[(nint)result] = id;
            _inputQueues[id] = new ConcurrentQueue<object>();

            RegisterInputCallbacks(result, id);

            _glfw.MakeContextCurrent(result);
            _glfw.MakeContextCurrent(null);

            done.Set();
        });

        done.WaitOne();
        return result;
    }

    public WindowHandle* CreateWindow(int x, int y, int w, int h, string title)
    {
        WindowHandle* result = null;
        using var done = new AutoResetEvent(false);

        EnqueueUi(() =>
        {
            ApplyCommonHints(_glfw!);
            result = _glfw!.CreateWindow(w, h, title, null, _shareRoot);
            if (result == null) throw new Exception("CreateWindow failed");

            _glfw.SetWindowPos(result, x, y);

            var id = Interlocked.Increment(ref _nextWindowId);
            _winToId[(nint)result] = id;
            _inputQueues[id] = new ConcurrentQueue<object>();

            RegisterInputCallbacks(result, id);

            _glfw.MakeContextCurrent(result);
            _glfw.MakeContextCurrent(null);

            done.Set();
        });

        done.WaitOne();
        return result;
    }

    public WindowHandle* CreateWindow(WindowPlacement placement, string title)
    {
        WindowHandle* result = null;
        using var done = new AutoResetEvent(false);

        EnqueueUi(() =>
        {
            ApplyCommonHints(_glfw!);

            if (placement.BorderlessFullscreen)
            {
                var mons = GetMonitorsInternal();
                var mi = mons[placement.MonitorIndex];

                _glfw!.WindowHint(WindowHintBool.Decorated, false);
                _glfw.WindowHint(WindowHintBool.Resizable, false);

                var w = placement.Width > 0 ? placement.Width : mi.Width;
                var h = placement.Height > 0 ? placement.Height : mi.Height;

                result = _glfw.CreateWindow(w, h, title, null, _shareRoot);
                if (result == null) throw new Exception("CreateWindow failed");

                _glfw.SetWindowPos(result, mi.X, mi.Y);
            }
            else
            {
                result = _glfw!.CreateWindow(placement.Width, placement.Height, title, null, _shareRoot);
                if (result != null) _glfw.SetWindowPos(result, placement.X, placement.Y);
            }

            if (result == null) throw new Exception("CreateWindow failed");

            var id = Interlocked.Increment(ref _nextWindowId);
            _winToId[(nint)result] = id;
            _inputQueues[id] = new ConcurrentQueue<object>();

            RegisterInputCallbacks(result, id);

            _glfw.MakeContextCurrent(result);
            _glfw.MakeContextCurrent(null);

            done.Set();
        });

        done.WaitOne();
        return result;
    }

    public WindowHandle* CreateHiddenWindow(int w, int h, string title = "hidden")
    {
        WindowHandle* result = null;
        using var done = new AutoResetEvent(false);

        EnqueueUi(() =>
        {
            ApplyCommonHints(_glfw!);
            _glfw!.WindowHint(WindowHintBool.Visible, false);

            result = _glfw.CreateWindow(w, h, title, null, _shareRoot);
            if (result == null) throw new Exception("CreateHiddenWindow failed");

            _glfw.HideWindow(result);

            _glfw.MakeContextCurrent(result);
            _glfw.MakeContextCurrent(null);

            done.Set();
        });

        done.WaitOne();
        return result;
    }

    public void DestroyWindow(WindowHandle* win)
    {
        if (win == null) return;
        EnqueueUi(() =>
        {
            if (_winToId.TryRemove((nint)win, out var id))
                _inputQueues.TryRemove(id, out _);

            _glfw!.MakeContextCurrent(null);
            _glfw.DestroyWindow(win);
        });
    }

    private void UiThreadMain()
    {
        _glfw = Glfw.GetApi();
        if (!_glfw.Init()) throw new Exception("GLFW init failed");

        ApplyCommonHints(_glfw);
        _shareRoot = _glfw.CreateWindow(1, 1, "share-root", null, null);
        if (_shareRoot == null) throw new Exception("Failed to create share root");
        _glfw.HideWindow(_shareRoot);

        _glfw.MakeContextCurrent(_shareRoot);
        _ = GL.GetApi(_glfw.GetProcAddress);
        _glfw.MakeContextCurrent(null);

        _started.Set();

        while (_running)
        {
            while (_uiJobs.TryDequeue(out var j))
            {
                try { j(); }
                catch (Exception ex) { Console.WriteLine($"[UI job error] {ex}"); }
            }

            _glfw.PollEvents();
            _work.WaitOne(1);
        }

        while (_uiJobs.TryDequeue(out var j))
        {
            try { j(); }
            catch (Exception ex) { Console.WriteLine($"[UI drain error] {ex}"); }
        }

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
        glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

        glfw.WindowHint(WindowHintInt.RedBits, 8);
        glfw.WindowHint(WindowHintInt.GreenBits, 8);
        glfw.WindowHint(WindowHintInt.BlueBits, 8);
        glfw.WindowHint(WindowHintInt.AlphaBits, 8);
        glfw.WindowHint(WindowHintInt.DepthBits, 24);
        glfw.WindowHint(WindowHintInt.StencilBits, 8);

        glfw.WindowHint(WindowHintBool.Resizable, true);
        glfw.WindowHint(WindowHintBool.Decorated, true);
        glfw.WindowHint(WindowHintBool.DoubleBuffer, true);
        glfw.WindowHint(WindowHintBool.TransparentFramebuffer, true);
        glfw.WindowHint(WindowHintBool.Visible, true);
    }

    public IReadOnlyList<MonitorInfo> GetMonitorsSafe()
    {
        IReadOnlyList<MonitorInfo>? result = null;
        using var done = new AutoResetEvent(false);
        EnqueueUi(() => { result = GetMonitorsInternal(); done.Set(); });
        done.WaitOne();
        return result!;
    }

    private IReadOnlyList<MonitorInfo> GetMonitorsInternal()
    {
        var monitors = new List<MonitorInfo>();
        var monitorPtrs = _glfw!.GetMonitors(out var count);
        for (var i = 0; i < count; i++)
        {
            var mPtr = monitorPtrs[i];
            var name = _glfw.GetMonitorName(mPtr) ?? "unknown";

            _glfw.GetMonitorPos(mPtr, out var mx, out var my);

            var modePtr = _glfw.GetVideoMode(mPtr);
            var width = modePtr->Width;
            var height = modePtr->Height;
            var refreshRate = modePtr->RefreshRate;

            _glfw.GetMonitorContentScale(mPtr, out var scaleX, out var scaleY);

            monitors.Add(new MonitorInfo((nint)mPtr, name, mx, my, width, height, scaleX, scaleY, refreshRate));
        }
        return monitors;
    }

    private void RegisterInputCallbacks(WindowHandle* win, int id)
    {
        _glfw!.SetCursorPosCallback(win, (w, x, y) => Enq(new MouseMoveEvent(id, x, y)));
        _glfw.SetMouseButtonCallback(win, (w, button, action, mods) => Enq(new MouseButtonEvent(id, button, action, mods)));
        _glfw.SetScrollCallback(win, (w, dx, dy) => Enq(new MouseWheelEvent(id, dx, dy)));
        _glfw.SetKeyCallback(win, (w, key, scancode, action, mods) => Enq(new KeyEvent(id, key, scancode, action, mods)));
        _glfw.SetCharCallback(win, (w, codepoint) => Enq(new CharEvent(id, codepoint)));
        return;

        void Enq(object e)
        {
            if (_inputQueues.TryGetValue(id, out var q))
                q.Enqueue(e);
        }
    }
}