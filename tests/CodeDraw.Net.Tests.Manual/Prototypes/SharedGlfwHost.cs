using System.Collections.Concurrent;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe class SharedGlfwHost : IDisposable
{
    private sealed class WindowCallbacks
    {
        public GlfwCallbacks.CursorPosCallback? CursorPos;
        public GlfwCallbacks.MouseButtonCallback? MouseButton;
        public GlfwCallbacks.ScrollCallback? Scroll;
        public GlfwCallbacks.KeyCallback? Key;
        public GlfwCallbacks.CharCallback? Char;
        public GlfwCallbacks.WindowCloseCallback? Close;
    }

    private readonly ConcurrentDictionary<nint, WindowCallbacks> _callbacks = new();

    public readonly record struct MouseMoveEvent(int WindowId, double X, double Y);

    public readonly record struct MouseButtonEvent(int WindowId, MouseButton Button, InputAction Action, KeyModifiers Mods);
    public readonly record struct MouseWheelEvent(int WindowId, double Dx, double Dy);
    public readonly record struct KeyEvent(int WindowId, Keys Key, int Scancode, InputAction Action, KeyModifiers Mods);
    public readonly record struct CharEvent(int WindowId, uint Codepoint);
    public readonly record struct WindowCloseRequestedEvent(int WindowId);

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

    private readonly ConcurrentDictionary<int, (int x, int y, int w, int h, bool valid)> _restoreRects = new();

    private readonly ConcurrentDictionary<int, ConcurrentQueue<object>> _inputQueues = new();

    private SharedGlfwHost() { }

    internal bool IsWindowAlive(WindowHandle* win)
        => win != null && _winToId.ContainsKey((nint)win);

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

        if (!_winToId.IsEmpty)
            Console.WriteLine($"[Host] Stop called with {_winToId.Count} windows still alive.");

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
        InvokeUi(() =>
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
        });
        return result;
    }

    public WindowHandle* CreateWindow(int x, int y, int w, int h, string title)
    {
        WindowHandle* result = null;
        InvokeUi(() =>
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
        });
        return result;
    }

    public WindowHandle* CreateWindow(WindowPlacement placement, string title)
    {
        WindowHandle* result = null;
        InvokeUi(() =>
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
        });
        return result;
    }

    public WindowHandle* CreateHiddenWindow(int w, int h, string title = "hidden")
    {
        WindowHandle* result = null;
        InvokeUi(() =>
        {
            ApplyCommonHints(_glfw!);
            _glfw!.WindowHint(WindowHintBool.Visible, false);

            result = _glfw.CreateWindow(w, h, title, null, _shareRoot);
            if (result == null) throw new Exception("CreateHiddenWindow failed");

            _glfw.HideWindow(result);

            _glfw.MakeContextCurrent(result);
            _glfw.MakeContextCurrent(null);
        });
        return result;
    }

    public void DestroyWindow(WindowHandle* win)
    {
        if (win == null) return;
        InvokeUi(() =>
        {
            _callbacks.TryRemove((nint)win, out _);

            if (_winToId.TryRemove((nint)win, out var id))
            {
                _inputQueues.TryRemove(id, out _);
                _restoreRects.TryRemove(id, out _);
            }

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
        InvokeUi(() => { result = GetMonitorsInternal(); });
        return result!;
    }

    private IReadOnlyList<MonitorInfo> GetMonitorsInternal()
    {
        var monitors = new List<MonitorInfo>();
        var monitorPointers = _glfw!.GetMonitors(out var count);
        for (var i = 0; i < count; i++)
        {
            var mPtr = monitorPointers[i];
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
        var cbs = new WindowCallbacks
        {
            CursorPos = (w, x, y) => Enq(new MouseMoveEvent(id, x, y)),
            MouseButton = (w, button, action, mods) => Enq(new MouseButtonEvent(id, button, action, mods)),
            Scroll = (w, dx, dy) => Enq(new MouseWheelEvent(id, dx, dy)),
            Key = (w, key, scancode, action, mods) => Enq(new KeyEvent(id, key, scancode, action, mods)),
            Char = (w, codepoint) => Enq(new CharEvent(id, codepoint)),
            Close = (w) =>
            {
                if (_winToId.TryGetValue((nint)w, out var wid))
                    Enq(new WindowCloseRequestedEvent(wid));
            }
        };

        _callbacks[(nint)win] = cbs;

        _glfw!.SetCursorPosCallback(win, cbs.CursorPos);
        _glfw.SetMouseButtonCallback(win, cbs.MouseButton);
        _glfw.SetScrollCallback(win, cbs.Scroll);
        _glfw.SetKeyCallback(win, cbs.Key);
        _glfw.SetCharCallback(win, cbs.Char);
        _glfw.SetWindowCloseCallback(win, cbs.Close);

        return;

        void Enq(object e)
        {
            if (_inputQueues.TryGetValue(id, out var q))
                q.Enqueue(e);
        }
    }

    public void SetBorderlessFullscreenSafe(WindowHandle* win, bool enabled)
    {
        InvokeUi(() =>
        {
            var mi = FindBestMonitorIndexForWindow(win);
            SetBorderlessFullscreenInternal(win, enabled, mi);
        });
    }

    public void SetBorderlessFullscreenSafe(WindowHandle* win, bool enabled, int monitorIndex)
    {
        InvokeUi(() =>
        {
            var mons = GetMonitorsInternal();
            if (mons.Count == 0) return;
            if (monitorIndex < 0 || monitorIndex >= mons.Count) monitorIndex = 0;

            SetBorderlessFullscreenInternal(win, enabled, monitorIndex);
        });
    }

    private void SetBorderlessFullscreenInternal(WindowHandle* win, bool enabled, int monitorIndex)
    {
        var glfw = _glfw!;
        var mons = GetMonitorsInternal();
        var m = mons[monitorIndex];

        var id = GetWindowId(win);

        if (enabled)
        {
            if (!_restoreRects.TryGetValue(id, out var rr) || !rr.valid)
            {
                glfw.GetWindowPos(win, out var x, out var y);
                glfw.GetWindowSize(win, out var w, out var h);
                _restoreRects[id] = (x, y, w, h, true);
            }

            glfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, false);
            glfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, false);

            glfw.SetWindowPos(win, m.X, m.Y);
            glfw.SetWindowSize(win, m.Width, m.Height);
        }
        else
        {
            glfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, true);
            glfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, true);

            if (_restoreRects.TryGetValue(id, out var rr) && rr.valid)
            {
                glfw.SetWindowPos(win, rr.x, rr.y);
                glfw.SetWindowSize(win, rr.w, rr.h);
            }
            else
            {
                glfw.SetWindowSize(win, 800, 450);
                glfw.SetWindowPos(win, 100, 100);
            }

            _restoreRects.TryRemove(id, out _);
        }
    }


    private void InvokeUi(Action action)
    {
        if (!_running) throw new InvalidOperationException("Host is not running.");

        var tcs = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        EnqueueUi(() =>
        {
            try { action(); tcs.TrySetResult(null); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });

        tcs.Task.GetAwaiter().GetResult();
    }

    private int FindBestMonitorIndexForWindow(WindowHandle* win)
    {
        // Must be called on UI thread (we call it inside InvokeUi)
        _glfw!.GetWindowPos(win, out var wx, out var wy);
        _glfw.GetWindowSize(win, out var ww, out var wh);

        // use center point
        var cx = wx + ww / 2;
        var cy = wy + wh / 2;

        var mons = GetMonitorsInternal();
        if (mons.Count == 0) return 0;

        // 1) if center inside a monitor => pick it
        for (int i = 0; i < mons.Count; i++)
        {
            var m = mons[i];
            if (cx >= m.X && cx < m.X + m.Width &&
                cy >= m.Y && cy < m.Y + m.Height)
                return i;
        }

        // 2) else pick monitor with max intersection area
        long bestArea = -1;
        int best = 0;

        int x1 = wx, y1 = wy, x2 = wx + ww, y2 = wy + wh;

        for (int i = 0; i < mons.Count; i++)
        {
            var m = mons[i];
            int mx1 = m.X, my1 = m.Y, mx2 = m.X + m.Width, my2 = m.Y + m.Height;

            int ix1 = Math.Max(x1, mx1);
            int iy1 = Math.Max(y1, my1);
            int ix2 = Math.Min(x2, mx2);
            int iy2 = Math.Min(y2, my2);

            int iw = Math.Max(0, ix2 - ix1);
            int ih = Math.Max(0, iy2 - iy1);

            long area = (long)iw * ih;
            if (area > bestArea)
            {
                bestArea = area;
                best = i;
            }
        }

        return best;
    }
}