using System.Collections.Concurrent;
using System.Diagnostics;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe class SharedGlfwHost : IDisposable
{
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

    private sealed record HostKeyEvent(nint WindowHandle, Keys Key, int Scancode, InputAction Action, ModifierKeys Mods)
        : HostInputEvent(WindowHandle);

    private sealed record HostMouseButtonEvent(nint WindowHandle, MouseButton Button, InputAction Action, ModifierKeys Mods)
        : HostInputEvent(WindowHandle);

    private sealed record HostScrollEvent(nint WindowHandle, double Dx, double Dy)
        : HostInputEvent(WindowHandle);

    private sealed record HostCursorPosEvent(nint WindowHandle, double X, double Y)
        : HostInputEvent(WindowHandle);

    // ---------- hub ----------
    public sealed class HostInputHub
    {
        // Key
        public event Action<CodeDrawWindow, Keys, ModifierKeys>? OnKeyDown;
        public event Action<CodeDrawWindow, Keys, ModifierKeys>? OnKeyUp;
        public event Action<CodeDrawWindow, Keys, ModifierKeys>? OnKeyRepeat;

        // Mouse
        public event Action<CodeDrawWindow, MouseButton, ModifierKeys>? OnMouseDown;
        public event Action<CodeDrawWindow, MouseButton, ModifierKeys>? OnMouseUp;

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
        public GlfwCallbacks.WindowPosCallback? WindowPos;
        public GlfwCallbacks.WindowSizeCallback? WindowSize;
        public GlfwCallbacks.FramebufferSizeCallback? FramebufferSize;
        public GlfwCallbacks.WindowMaximizeCallback? Maximize;
        public GlfwCallbacks.WindowIconifyCallback? Iconify;
    }

    private readonly ConcurrentDictionary<nint, WindowCallbacks> _callbacks = new();

    public readonly record struct MouseMoveEvent(int WindowId, double X, double Y);
    public readonly record struct MouseButtonEvent(int WindowId, MouseButton Button, InputAction Action, ModifierKeys Mods);
    public readonly record struct MouseWheelEvent(int WindowId, double Dx, double Dy);
    public readonly record struct KeyEvent(int WindowId, Keys Key, int Scancode, InputAction Action, ModifierKeys Mods);
    public readonly record struct CharEvent(int WindowId, uint Codepoint);
    public readonly record struct WindowCloseRequestedEvent(int WindowId);
    public readonly record struct WindowPosEvent(int WindowId, int X, int Y);
    public readonly record struct WindowSizeEvent(int WindowId, int W, int H);
    public readonly record struct FramebufferSizeEvent(int WindowId, int W, int H);
    public readonly record struct WindowMaximizedEvent(int WindowId, bool IsMaximized);
    public readonly record struct WindowIconifiedEvent(int WindowId, bool IsIconified);


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

    public WindowHandle* ShareRoot { get; private set; } = null;

    private Thread? _hostThread;
    private int _hostThreadId;
    private bool IsHostThread => Environment.CurrentManagedThreadId == _hostThreadId;
    private volatile bool _running;
    private readonly AutoResetEvent _started = new(false);
    private readonly AutoResetEvent _work = new(false);
    private readonly ConcurrentQueue<Action> _hostJobs = new();

    private int _nextWindowId;
    private readonly ConcurrentDictionary<nint, int> _winToId = new();
    private readonly ConcurrentDictionary<int, ConcurrentQueue<object>> _inputQueues = new();

    private readonly WindowStateMachine _stateMachine = new();
    private readonly ConcurrentDictionary<int, long> _lastResizeTick = new();
    private readonly ConcurrentDictionary<int, int> _isLiveResize = new(); // 0/1

    private List<MonitorInfo> _monitorsCache = [];
    private int _monitorsDirty = 1; // start dirty so we build once

    private GlfwCallbacks.MonitorCallback? _monitorCallback; // keep delegate alive
    
    private static long NowTicks() => Stopwatch.GetTimestamp();
    private static double TicksToMs(long dt) => dt * 1000.0 / Stopwatch.Frequency;

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

        if (IsHostThread)
        {
            action(); // we are already on the host thread, just execute directly
            return;
        }

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
            ApplyCommonHints();
            result = LockedGlfw.CreateWindow(w, h, title, null, ShareRoot);
            if (result == null) throw new Exception("CreateWindow failed");
            EnsureHostQueue(result);

            LockedGlfw.SetWindowPos(result, x, y);

            var id = Interlocked.Increment(ref _nextWindowId);
            _winToId[(nint)result] = id;
            _inputQueues[id] = new ConcurrentQueue<object>();

            RegisterInputCallbacks(result, id);

            LockedGlfw.MakeContextCurrent(result);
            LockedGlfw.MakeContextCurrent(null);
        });
        return result;
    }

    public WindowHandle* CreateHiddenWindow(int w, int h, string title = "hidden")
    {
        WindowHandle* result = null;
        InvokeHostSync(() =>
        {
            ApplyCommonHints();
            LockedGlfw.WindowHint(WindowHintBool.Visible, false);

            result = LockedGlfw.CreateWindow(w, h, title, null, ShareRoot);
            if (result == null) throw new Exception("CreateHiddenWindow failed");
            EnsureHostQueue(result);

            LockedGlfw.HideWindow(result);

            LockedGlfw.MakeContextCurrent(result);
            LockedGlfw.MakeContextCurrent(null);
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

            if (_winToId.TryRemove((nint)win, out var id))
            {
                _inputQueues.TryRemove(id, out _);
            }

            LockedGlfw.MakeContextCurrent(null);
            LockedGlfw.DestroyWindow(win);

        });
    }

    private List<MonitorInfo> GetMonitorsCached_HostThreadUnsafe()
    {
        // host thread only (PollEvents + jobs), so no heavy locking needed
        if (Volatile.Read(ref _monitorsDirty) == 0)
            return _monitorsCache;

        Volatile.Write(ref _monitorsDirty, 0);
        _monitorsCache = BuildMonitorsList_HostThreadUnsafe();
        return _monitorsCache;
    }

    private static List<MonitorInfo> BuildMonitorsList_HostThreadUnsafe()
    {
        var monitors = new List<MonitorInfo>();
        var monitorPointers = LockedGlfw.GetMonitors(out var count);
        for (var i = 0; i < count; i++)
        {
            var mPtr = monitorPointers[i];
            var name = LockedGlfw.GetMonitorName(mPtr);

            var modePtr = LockedGlfw.GetVideoMode(mPtr);
            var refreshRate = modePtr->RefreshRate;

            LockedGlfw.GetMonitorWorkarea(mPtr, out var wx, out var wy, out var ww, out var wh);
            LockedGlfw.GetMonitorContentScale(mPtr, out var scaleX, out var scaleY);

            monitors.Add(new MonitorInfo((nint)mPtr, name, wx, wy, ww, wh, scaleX, scaleY, refreshRate));
        }
        return monitors;
    }

    // --------------------------

    private void HostThreadMain()
    {
        _hostThreadId = Environment.CurrentManagedThreadId;
        LockedGlfw.SetGlfwInstance(Glfw.GetApi());
        if (!LockedGlfw.Init()) throw new Exception("GLFW init failed");

        ApplyCommonHints();
        ShareRoot = LockedGlfw.CreateWindow(1, 1, "share-root", null, null);
        if (ShareRoot == null) throw new Exception("Failed to create share root");
        LockedGlfw.HideWindow(ShareRoot);

        LockedGlfw.MakeContextCurrent(ShareRoot);
        _ = GL.GetApi(LockedGlfw.GetProcAddress);
        LockedGlfw.MakeContextCurrent(null);

        _monitorCallback = (mon, state) =>
        {
            _monitorsDirty = 1;
        };
        LockedGlfw.SetMonitorCallback(_monitorCallback);
        
        _started.Set();

        while (_running)
        {
            while (_hostJobs.TryDequeue(out var j))
            {
                try { j(); }
                catch (Exception ex) { Console.WriteLine($"[Host job error] {ex}"); }
            }

            LockedGlfw.PollEvents();
            _work.WaitOne(1);
        }

        while (_hostJobs.TryDequeue(out var j))
        {
            try { j(); }
            catch (Exception ex) { Console.WriteLine($"[Host drain error] {ex}"); }
        }

        if (ShareRoot != null)
        {
            LockedGlfw.MakeContextCurrent(null);
            LockedGlfw.DestroyWindow(ShareRoot);
            ShareRoot = null;
        }
        
        _monitorCallback = null;

        LockedGlfw.Terminate();
        LockedGlfw.SetGlfwInstance(null);
    }

    private static void ApplyCommonHints()
    {
        LockedGlfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        LockedGlfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        LockedGlfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

        LockedGlfw.WindowHint(WindowHintInt.RedBits, 8);
        LockedGlfw.WindowHint(WindowHintInt.GreenBits, 8);
        LockedGlfw.WindowHint(WindowHintInt.BlueBits, 8);
        LockedGlfw.WindowHint(WindowHintInt.AlphaBits, 8);
        LockedGlfw.WindowHint(WindowHintInt.DepthBits, 24);
        LockedGlfw.WindowHint(WindowHintInt.StencilBits, 8);

        LockedGlfw.WindowHint(WindowHintBool.Resizable, true);
        LockedGlfw.WindowHint(WindowHintBool.Decorated, true);
        LockedGlfw.WindowHint(WindowHintBool.DoubleBuffer, true);
        LockedGlfw.WindowHint(WindowHintBool.TransparentFramebuffer, true);
        LockedGlfw.WindowHint(WindowHintBool.Visible, true);
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
                var keyMods = (ModifierKeys)mods;
                Enq(new MouseButtonEvent(id, button, action, keyMods));
                EnqueueHostInput(new HostMouseButtonEvent((nint)w, button, action, keyMods));
            },
            Scroll = (w, dx, dy) =>
            {
                Enq(new MouseWheelEvent(id, dx, dy));
                EnqueueHostInput(new HostScrollEvent((nint)w, dx, dy));
            },
            Key = (w, key, scancode, action, mods) =>
            {
                var keyMods = (ModifierKeys)mods;
                Enq(new KeyEvent(id, key, scancode, action, keyMods));
                EnqueueHostInput(new HostKeyEvent((nint)w, key, scancode, action, keyMods));
            },
            Char = (w, codepoint) => Enq(new CharEvent(id, codepoint)),
            Close = (w) =>
            {
                if (_winToId.TryGetValue((nint)w, out var wid))
                    Enq(new WindowCloseRequestedEvent(wid));
            },
            WindowPos = (w, x, y) => Enq(new WindowPosEvent(id, x, y)),
            WindowSize = (w, wpx, hpx) =>
            {
                NotifyWindowResized(id);
                Enq(new WindowSizeEvent(id, wpx, hpx));
            },
            FramebufferSize = (w, wpx, hpx) =>
            {
                NotifyWindowResized(id);
                Enq(new FramebufferSizeEvent(id, wpx, hpx));
            },
            Maximize = (w, maximized) =>
            {
                Enq(new WindowMaximizedEvent(id, maximized));
            },

            Iconify = (w, iconified) =>
            {
                Enq(new WindowIconifiedEvent(id, iconified));
            },
        };

        _callbacks[(nint)win] = cbs;

        LockedGlfw.SetInputMode(win, (StickyAttributes)0x00033004, true); //capslock support (https://www.glfw.org/docs/3.3/glfw3_8h.html#a07b84de0b52143e1958f88a7d9105947)
        LockedGlfw.SetCursorPosCallback(win, cbs.CursorPos);
        LockedGlfw.SetMouseButtonCallback(win, cbs.MouseButton);
        LockedGlfw.SetScrollCallback(win, cbs.Scroll);
        LockedGlfw.SetKeyCallback(win, cbs.Key);
        LockedGlfw.SetCharCallback(win, cbs.Char);
        LockedGlfw.SetWindowCloseCallback(win, cbs.Close);
        LockedGlfw.SetWindowPosCallback(win, cbs.WindowPos);
        LockedGlfw.SetWindowSizeCallback(win, cbs.WindowSize);
        LockedGlfw.SetFramebufferSizeCallback(win, cbs.FramebufferSize);
        LockedGlfw.SetWindowMaximizeCallback(win, cbs.Maximize);
        LockedGlfw.SetWindowIconifyCallback(win, cbs.Iconify);

        return;

        void Enq(object e)
        {
            if (_inputQueues.TryGetValue(id, out var q))
                q.Enqueue(e);
        }
    }

    internal void ApplyWindowSettingsSync(WindowHandle* win, int windowId, WindowSettingsSnapshot desired, WindowDirty dirty)
    {
        if (win == null) return;

        InvokeHostSync(() =>
        {
            if (!IsWindowAlive(win)) return;

            var monitors = GetMonitorsCached_HostThreadUnsafe();

            _stateMachine.Apply(
                win,
                windowId,
                desired,
                dirty,
                monitors
            );
        });
    }

    internal void NotifyWindowResized(int windowId)
    {
        _lastResizeTick[windowId] = NowTicks();
        _isLiveResize[windowId] = 1;
    }

    internal bool IsWindowInLiveResize(int windowId, double graceMs = 200.0)
    {
        if (!_isLiveResize.TryGetValue(windowId, out var live) || live == 0)
            return false;

        if (!_lastResizeTick.TryGetValue(windowId, out var t))
            return false;

        var dtMs = TicksToMs(NowTicks() - t);
        if (dtMs <= graceMs) return true;

        // auto-clear
        _isLiveResize[windowId] = 0;
        return false;
    }
}