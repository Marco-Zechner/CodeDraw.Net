using System.Collections.Concurrent;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe class SharedGlfwHost : IDisposable
{
    private readonly ConcurrentDictionary<nint, object> _windowLocks = new();

    internal object GetWindowLock(WindowHandle* win)
    {
        return win == null ? new object() : _windowLocks.GetOrAdd((nint)win, _ => new object());
    }

    public HostInputHub Input { get; } = new();

    private readonly ConcurrentDictionary<nint, ConcurrentQueue<HostInputEvent>> _hostInputByWindow = new();

    private void EnsureHostQueue(WindowHandle* win)
    {
        if (win == null) return;
        _hostInputByWindow.TryAdd((nint)win, new ConcurrentQueue<HostInputEvent>());
    }

    private void RemoveHostQueue(WindowHandle* win)
    {
        if (win == null) return;
        _hostInputByWindow.TryRemove((nint)win, out _);
    }

    private void EnqueueHostInput(HostInputEvent e)
    {
        if (_hostInputByWindow.TryGetValue(e.WindowHandle, out var q))
            q.Enqueue(e);
    }

    public void PumpHostInputForWindow(CodeDrawWindow windowObj, int max = 10_000)
    {
        if (windowObj.IsDisposed) return;

        var handle = windowObj.WindowHandle;
        if (!_hostInputByWindow.TryGetValue(handle, out var q)) return;

        var n = 0;
        while (n++ < max && q.TryDequeue(out var e))
            Input.Dispatch(windowObj, e);
    }

    // ---------- event types ----------
    internal abstract record HostInputEvent(nint WindowHandle);

    private sealed record HostKeyEvent(nint WindowHandle, Keys Key, int Scancode, InputAction Action, KeyModifiers Mods)
        : HostInputEvent(WindowHandle);

    private sealed record HostMouseButtonEvent(nint WindowHandle, MouseButton Button, InputAction Action, KeyModifiers Mods)
        : HostInputEvent(WindowHandle);

    private sealed record HostScrollEvent(nint WindowHandle, double Dx, double Dy)
        : HostInputEvent(WindowHandle);

    private sealed record HostCursorPosEvent(nint WindowHandle, double X, double Y)
        : HostInputEvent(WindowHandle);

    // ---------- hub ----------
    public sealed class HostInputHub
    {
        // Key
        public event Action<CodeDrawWindow, Keys, KeyModifiers>? OnKeyDown;
        public event Action<CodeDrawWindow, Keys, KeyModifiers>? OnKeyUp;
        public event Action<CodeDrawWindow, Keys, KeyModifiers>? OnKeyRepeat;

        // Mouse
        public event Action<CodeDrawWindow, MouseButton, KeyModifiers>? OnMouseDown;
        public event Action<CodeDrawWindow, MouseButton, KeyModifiers>? OnMouseUp;

        // Wheel / move
        public event Action<CodeDrawWindow, double, double>? OnScroll;
        public event Action<CodeDrawWindow, double, double>? OnMouseMove;

        internal void Dispatch(CodeDrawWindow win, HostInputEvent e)
        {
            switch (e)
            {
                case HostKeyEvent ke:
                    switch (ke.Action)
                    {
                        case InputAction.Press: OnKeyDown?.Invoke(win, ke.Key, ke.Mods); break;
                        case InputAction.Release: OnKeyUp?.Invoke(win, ke.Key, ke.Mods); break;
                        case InputAction.Repeat: OnKeyRepeat?.Invoke(win, ke.Key, ke.Mods); break;
                    }
                    break;

                case HostMouseButtonEvent mb:
                    if (mb.Action == InputAction.Press)  OnMouseDown?.Invoke(win, mb.Button, mb.Mods);
                    else if (mb.Action == InputAction.Release) OnMouseUp?.Invoke(win, mb.Button, mb.Mods);
                    break;

                case HostScrollEvent sc:
                    OnScroll?.Invoke(win, sc.Dx, sc.Dy);
                    break;

                case HostCursorPosEvent mv:
                    OnMouseMove?.Invoke(win, mv.X, mv.Y);
                    break;
            }
        }
    }

    private readonly ConcurrentDictionary<nint, WeakReference<CodeDrawWindow>> _winToObj = new();

    internal void RegisterWindowObject(WindowHandle* win, CodeDrawWindow obj)
        => _winToObj[(nint)win] = new WeakReference<CodeDrawWindow>(obj);

    internal void UnregisterWindowObject(WindowHandle* win)
        => _winToObj.TryRemove((nint)win, out _);

    internal CodeDrawWindow? TryGetWindowObject(WindowHandle* win)
    {
        if (win == null) return null;
        if (!_winToObj.TryGetValue((nint)win, out var wr)) return null;
        return wr.TryGetTarget(out var w) ? w : null;
    }

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
        int WorkX, int WorkY,
        int WorkWidth, int WorkHeight,
        float ContentScaleX,
        float ContentScaleY,
        int RefreshRate
    );

    public static SharedGlfwHost Instance { get; } = new();

    public Glfw Glfw => _glfw!;
    public WindowHandle* ShareRoot => _shareRoot;

    private Thread? _hostThread;
    private Glfw? _glfw;
    private volatile bool _running;
    private readonly AutoResetEvent _started = new(false);
    private readonly AutoResetEvent _work = new(false);
    private readonly ConcurrentQueue<Action> _hostJobs = new();

    private WindowHandle* _shareRoot = null;

    private int _nextWindowId;
    private readonly ConcurrentDictionary<nint, int> _winToId = new();
    private readonly ConcurrentDictionary<int, ConcurrentQueue<object>> _inputQueues = new();

    // stores original rect while a window is "maximized borderless"
    private readonly ConcurrentDictionary<int, (int x, int y, int w, int h, bool valid)> _restoreRects = new();

    private SharedGlfwHost() { }

    internal bool IsWindowAlive(WindowHandle* win)
        => win != null && _winToId.ContainsKey((nint)win);

    public void Start()
    {
        if (_running) return;
        _running = true;
        _hostThread = new Thread(HostThreadMain) { IsBackground = true, Name = "GLFW-HOST" };
        _hostThread.Start();
        _started.WaitOne();
    }

    public void Dispose() => Stop();

    public void Stop()
    {
        if (!_running) return;

        if (!_winToId.IsEmpty)
            Console.WriteLine($"[Host] Stop called with {_winToId.Count} windows still alive.");

        InvokeHostAsync(() => _running = false);
        _hostThread?.Join();
        _hostThread = null;
    }

    public void InvokeHostAsync(Action job)
    {
        _hostJobs.Enqueue(job);
        _work.Set();
    }

    private void InvokeHostSync(Action action)
    {
        if (!_running) throw new InvalidOperationException("Host is not running.");

        var tcs = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        InvokeHostAsync(() =>
        {
            try { action(); tcs.TrySetResult(null); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });

        tcs.Task.GetAwaiter().GetResult();
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
        => CreateWindow(50, 120, w, h, title);

    public WindowHandle* CreateWindow(int x, int y, int w, int h, string title)
    {
        WindowHandle* result = null;
        InvokeHostSync(() =>
        {
            ApplyCommonHints(_glfw!);
            result = _glfw!.CreateWindow(w, h, title, null, _shareRoot);
            if (result == null) throw new Exception("CreateWindow failed");
            _windowLocks.TryAdd((nint)result, new object());
            EnsureHostQueue(result);

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

    public WindowHandle* CreateHiddenWindow(int w, int h, string title = "hidden")
    {
        WindowHandle* result = null;
        InvokeHostSync(() =>
        {
            ApplyCommonHints(_glfw!);
            _glfw!.WindowHint(WindowHintBool.Visible, false);

            result = _glfw.CreateWindow(w, h, title, null, _shareRoot);
            if (result == null) throw new Exception("CreateHiddenWindow failed");
            _windowLocks.TryAdd((nint)result, new object());
            EnsureHostQueue(result);

            _glfw.HideWindow(result);

            _glfw.MakeContextCurrent(result);
            _glfw.MakeContextCurrent(null);
        });
        return result;
    }

    public void DestroyWindow(WindowHandle* win)
    {
        if (win == null) return;
        InvokeHostSync(() =>
        {
            _callbacks.TryRemove((nint)win, out _);
            UnregisterWindowObject(win);
            RemoveHostQueue(win);
            _windowLocks.TryRemove((nint)win, out _);

            if (_winToId.TryRemove((nint)win, out var id))
            {
                _inputQueues.TryRemove(id, out _);
                _restoreRects.TryRemove(id, out _);
            }

            _glfw!.MakeContextCurrent(null);
            _glfw.DestroyWindow(win);
        });
    }

    // --------------------------
    // MaximizeBorderless API
    // --------------------------

    public void SetMaximizeBorderlessSafe(WindowHandle* win, bool enabled)
    {
        InvokeHostSync(() =>
        {
            if (!IsWindowAlive(win)) return;
            var mi = FindBestMonitorIndexForWindow_HostThreadUnsafe(win);
            SetMaximizeBorderlessInternal_HostThreadUnsafe(win, enabled, mi);
        });
    }

    public void SetMaximizeBorderlessSafe(WindowHandle* win, bool enabled, int monitorIndex)
    {
        InvokeHostSync(() =>
        {
            if (!IsWindowAlive(win)) return;
            SetMaximizeBorderlessInternal_HostThreadUnsafe(win, enabled, monitorIndex);
        });
    }

    private void SetMaximizeBorderlessInternal_HostThreadUnsafe(WindowHandle* win, bool enabled, int monitorIndex)
    {
        var l = GetWindowLock(win);
        lock (l)
        {
            var glfw = _glfw!;
            var mons = GetMonitorsInternal_HostThreadUnsafe(glfw);
            if (mons.Count == 0) return;
            if (monitorIndex < 0 || monitorIndex >= mons.Count) monitorIndex = 0;

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

                glfw.SetWindowPos(win, m.WorkX, m.WorkY);
                glfw.SetWindowSize(win, m.WorkWidth, m.WorkHeight);
            }
            else
            {
                glfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, true);

                if (_restoreRects.TryGetValue(id, out var rr) && rr.valid)
                {
                    glfw.SetWindowPos(win, rr.x, rr.y);
                    glfw.SetWindowSize(win, rr.w, rr.h);
                }

                _restoreRects.TryRemove(id, out _);
            }

            glfw.FocusWindow(win);
        }
    }

    private static List<MonitorInfo> GetMonitorsInternal_HostThreadUnsafe(Glfw glfw)
    {
        var monitors = new List<MonitorInfo>();
        var monitorPointers = glfw.GetMonitors(out var count);
        for (var i = 0; i < count; i++)
        {
            var mPtr = monitorPointers[i];
            var name = glfw.GetMonitorName(mPtr) ?? "unknown";

            var modePtr = glfw.GetVideoMode(mPtr);
            var refreshRate = modePtr->RefreshRate;

            glfw.GetMonitorWorkarea(mPtr, out var wx, out var wy, out var ww, out var wh);

            glfw.GetMonitorContentScale(mPtr, out var scaleX, out var scaleY);

            monitors.Add(new MonitorInfo((nint)mPtr, name, wx, wy, ww, wh, scaleX, scaleY, refreshRate));
        }
        return monitors;
    }

    private int FindBestMonitorIndexForWindow_HostThreadUnsafe(WindowHandle* win)
    {
        _glfw!.GetWindowPos(win, out var wx, out var wy);
        _glfw.GetWindowSize(win, out var ww, out var wh);

        var cx = wx + ww / 2;
        var cy = wy + wh / 2;

        var mons = GetMonitorsInternal_HostThreadUnsafe(_glfw!);
        if (mons.Count == 0) return 0;

        for (var i = 0; i < mons.Count; i++)
        {
            var m = mons[i];
            if (cx >= m.WorkX && cx < m.WorkX + m.WorkWidth &&
                cy >= m.WorkY && cy < m.WorkY + m.WorkHeight)
                return i;
        }

        long bestArea = -1;
        var best = 0;

        int x1 = wx, y1 = wy, x2 = wx + ww, y2 = wy + wh;

        for (var i = 0; i < mons.Count; i++)
        {
            var m = mons[i];
            int mx1 = m.WorkX, my1 = m.WorkY, mx2 = m.WorkX + m.WorkWidth, my2 = m.WorkY + m.WorkHeight;

            var ix1 = Math.Max(x1, mx1);
            var iy1 = Math.Max(y1, my1);
            var ix2 = Math.Min(x2, mx2);
            var iy2 = Math.Min(y2, my2);

            var iw = Math.Max(0, ix2 - ix1);
            var ih = Math.Max(0, iy2 - iy1);

            var area = (long)iw * ih;
            if (area <= bestArea) continue;

            bestArea = area;
            best = i;
        }

        return best;
    }

    // --------------------------

    private void HostThreadMain()
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
            while (_hostJobs.TryDequeue(out var j))
            {
                try { j(); }
                catch (Exception ex) { Console.WriteLine($"[Host job error] {ex}"); }
            }

            _glfw.PollEvents();
            _work.WaitOne(1);
        }

        while (_hostJobs.TryDequeue(out var j))
        {
            try { j(); }
            catch (Exception ex) { Console.WriteLine($"[Host drain error] {ex}"); }
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

    private void RegisterInputCallbacks(WindowHandle* win, int id)
    {
        var cbs = new WindowCallbacks
        {
            CursorPos = (w, x, y) =>
            {
                Enq(new MouseMoveEvent(id, x, y));
                EnqueueHostInput(new HostCursorPosEvent((nint)w, x, y));
            },
            MouseButton = (w, button, action, mods) =>
            {
                Enq(new MouseButtonEvent(id, button, action, mods));
                EnqueueHostInput(new HostMouseButtonEvent((nint)w, button, action, mods));
            },
            Scroll = (w, dx, dy) =>
            {
                Enq(new MouseWheelEvent(id, dx, dy));
                EnqueueHostInput(new HostScrollEvent((nint)w, dx, dy));
            },
            Key = (w, key, scancode, action, mods) =>
            {
                Enq(new KeyEvent(id, key, scancode, action, mods));
                EnqueueHostInput(new HostKeyEvent((nint)w, key, scancode, action, mods));
            },
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
}