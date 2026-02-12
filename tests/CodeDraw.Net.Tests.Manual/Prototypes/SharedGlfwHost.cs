using System.Collections.Concurrent;
using System.Diagnostics;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe partial class SharedGlfwHost : IDisposable
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

    public WindowHandle* ShareRoot => _shareRoot;

    private Thread? _hostThread;
    private int _hostThreadId;
    private bool IsHostThread => Environment.CurrentManagedThreadId == _hostThreadId;
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
    private readonly ConcurrentDictionary<int, (int x, int y, int w, int h, bool valid)> _fullscreenRestoreRects = new();
    private readonly ConcurrentDictionary<int, long> _lastResizeTick = new();
    private readonly ConcurrentDictionary<int, int> _isLiveResize = new(); // 0/1

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
            result = LockedGlfw.CreateWindow(w, h, title, null, _shareRoot);
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

            result = LockedGlfw.CreateWindow(w, h, title, null, _shareRoot);
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
                _restoreRects.TryRemove(id, out _);
            }

            LockedGlfw.MakeContextCurrent(null);
            LockedGlfw.DestroyWindow(win);

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
        var mons = GetMonitorsInternal_HostThreadUnsafe();
        if (mons.Count == 0) return;
        if (monitorIndex < 0 || monitorIndex >= mons.Count) monitorIndex = 0;

        var m = mons[monitorIndex];
        var id = GetWindowId(win);

        if (enabled)
        {
            if (!_restoreRects.TryGetValue(id, out var rr) || !rr.valid)
            {
                LockedGlfw.GetWindowPos(win, out var x, out var y);
                LockedGlfw.GetWindowSize(win, out var w, out var h);
                _restoreRects[id] = (x, y, w, h, true);
            }

            LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, false);

            LockedGlfw.SetWindowPos(win, m.WorkX, m.WorkY);
            LockedGlfw.SetWindowSize(win, m.WorkWidth, m.WorkHeight);
        }
        else
        {
            LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, true);

            if (_restoreRects.TryGetValue(id, out var rr) && rr.valid)
            {
                LockedGlfw.SetWindowPos(win, rr.x, rr.y);
                LockedGlfw.SetWindowSize(win, rr.w, rr.h);
            }

            _restoreRects.TryRemove(id, out _);
        }

        LockedGlfw.FocusWindow(win);

    }

    private static List<MonitorInfo> GetMonitorsInternal_HostThreadUnsafe()
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

    private int FindBestMonitorIndexForWindow_HostThreadUnsafe(WindowHandle* win)
    {
        LockedGlfw.GetWindowPos(win, out var wx, out var wy);
        LockedGlfw.GetWindowSize(win, out var ww, out var wh);

        var cx = wx + ww / 2;
        var cy = wy + wh / 2;

        var mons = GetMonitorsInternal_HostThreadUnsafe();
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
        _hostThreadId = Environment.CurrentManagedThreadId;
        LockedGlfw.SetGlfwInstance(Glfw.GetApi());
        if (!LockedGlfw.Init()) throw new Exception("GLFW init failed");

        ApplyCommonHints();
        _shareRoot = LockedGlfw.CreateWindow(1, 1, "share-root", null, null);
        if (_shareRoot == null) throw new Exception("Failed to create share root");
        LockedGlfw.HideWindow(_shareRoot);

        LockedGlfw.MakeContextCurrent(_shareRoot);
        _ = GL.GetApi(LockedGlfw.GetProcAddress);
        LockedGlfw.MakeContextCurrent(null);

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

        if (_shareRoot != null)
        {
            LockedGlfw.MakeContextCurrent(null);
            LockedGlfw.DestroyWindow(_shareRoot);
            _shareRoot = null;
        }

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

            desired = desired.Normalize();

            // Title
            if ((dirty & WindowDirty.Title) != 0)
                LockedGlfw.SetWindowTitle(win, desired.Title);

            // AlwaysOnTop
            if ((dirty & WindowDirty.AlwaysOnTop) != 0)
                LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Floating, desired.AlwaysOnTop);

            // ClickThrough
            if ((dirty & WindowDirty.ClickThrough) != 0)
                TryApplyClickThrough_HostThreadUnsafe(win, desired.ClickThrough);

            // State first (because it can force border/constraints behavior)
            if ((dirty & WindowDirty.WindowState) != 0)
                ApplyWindowState_HostThreadUnsafe(win, windowId, desired);

            // Frame/resizability + constraints
            if ((dirty & WindowDirty.Border) != 0)
            {
                ApplyFrameAndResizability_HostThreadUnsafe(win, desired);
                ApplyConstraintsIfWindowed_HostThreadUnsafe(win, desired);
            }

            // Pos/Size only if Windowed
            if (desired.State == WindowState.Windowed)
            {
                if ((dirty & WindowDirty.WindowPos) != 0)
                    LockedGlfw.SetWindowPos(win, desired.WindowPosition.X, desired.WindowPosition.Y);

                if ((dirty & WindowDirty.CanvasSize) != 0)
                    LockedGlfw.SetWindowSize(win, desired.Size.X, desired.Size.Y);
            }

            LockedGlfw.FocusWindow(win);
        });
    }

    private void ApplyFrameAndResizability_HostThreadUnsafe(WindowHandle* win, WindowSettingsSnapshot d)
    {
        // Decorations
        LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, d.FrameMode == WindowFrameMode.Decorated);

        // Resizable flag only depends on resize mode (not frame)
        var resizable = d.ResizeMode != WindowResizeMode.Fixed;
        LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, resizable);
    }

    private void ClearAllConstraints_HostThreadUnsafe(WindowHandle* win)
    {
        LockedGlfw.SetWindowSizeLimits(win, Glfw.DontCare, Glfw.DontCare, Glfw.DontCare, Glfw.DontCare);
        LockedGlfw.SetWindowAspectRatio(win, Glfw.DontCare, Glfw.DontCare);
    }

    private void ApplyConstraintsIfWindowed_HostThreadUnsafe(WindowHandle* win, WindowSettingsSnapshot d)
    {
        ClearAllConstraints_HostThreadUnsafe(win);

        if (d.State != WindowState.Windowed || d.ResizeMode == WindowResizeMode.Fixed || d.ResizeMode == WindowResizeMode.Resizable)
            return;

        // Resizable/Fixed => no constraints
        switch (d.ResizeMode)
        {
            default:
            case WindowResizeMode.Limited:
                LockedGlfw.SetWindowSizeLimits(win, d.MinSize.X, d.MinSize.Y, d.MaxSize.X, d.MaxSize.Y);
                break;
            case WindowResizeMode.Aspect:
                LockedGlfw.SetWindowAspectRatio(win, d.AspectRatio.X, d.AspectRatio.Y);
                break;
        }
    }

    private void ApplyWindowState_HostThreadUnsafe(WindowHandle* win, int windowId, WindowSettingsSnapshot desired)
    {
        switch (desired.State)
        {
            case WindowState.Windowed:
                ExitFullscreenIfNeeded(win, windowId);
                LockedGlfw.RestoreWindow(win);
                break;

            case WindowState.Minimized:
                ExitFullscreenIfNeeded(win, windowId);
                LockedGlfw.IconifyWindow(win);
                break;

            case WindowState.Maximized:
                ExitFullscreenIfNeeded(win, windowId);
                ClearAllConstraints_HostThreadUnsafe(win);
                ApplyFrameAndResizability_HostThreadUnsafe(win, desired);
                ApplyMaximizeWorkarea_HostThreadUnsafe(win, decorated: desired.FrameMode == WindowFrameMode.Decorated);
                break;

            case WindowState.Fullscreen:
                EnterFullscreen(win, windowId);
                break;
        }
    }

    private void ApplyMaximizeWorkarea_HostThreadUnsafe(WindowHandle* win, bool decorated)
    {
        var mi = FindBestMonitorIndexForWindow_HostThreadUnsafe(win);
        var mons = GetMonitorsInternal_HostThreadUnsafe();
        if (mons.Count == 0)
            return;

        var m = mons[Math.Clamp(mi, 0, mons.Count - 1)];

        // Base target: workarea
        var x = m.WorkX;
        var y = m.WorkY;
        var w = m.WorkWidth;
        var h = m.WorkHeight;

        if (decorated)
        {
            // IMPORTANT: compensate for window chrome so the client fills the workarea.
            LockedGlfw.GetWindowFrameSize(win, out var left, out var top, out var right, out var bottom);

            x -= left;
            y -= top;
            w += left + right;
            h += top + bottom;
        }

        // Make sure we're not in an OS-maximized state; we are doing manual maximize.
        LockedGlfw.RestoreWindow(win);

        LockedGlfw.SetWindowPos(win, x, y);
        LockedGlfw.SetWindowSize(win, w, h);
        LockedGlfw.FocusWindow(win);
    }

    private void EnterFullscreen(WindowHandle* win, int windowId)
    {
        // save restore rect once
        if (!_fullscreenRestoreRects.TryGetValue(windowId, out var rr) || !rr.valid)
        {
            LockedGlfw.GetWindowPos(win, out var x, out var y);
            LockedGlfw.GetWindowSize(win, out var w, out var h);
            _fullscreenRestoreRects[windowId] = (x, y, w, h, true);
        }

        // clear constraints
        LockedGlfw.SetWindowSizeLimits(win, Glfw.DontCare, Glfw.DontCare, Glfw.DontCare, Glfw.DontCare);
        LockedGlfw.SetWindowAspectRatio(win, Glfw.DontCare, Glfw.DontCare);

        var mi = FindBestMonitorIndexForWindow_HostThreadUnsafe(win);
        var monitors = LockedGlfw.GetMonitors(out var count);
        if (count <= 0 || monitors == null) return;
        mi = Math.Clamp(mi, 0, count - 1);
        var m = monitors[mi];

        LockedGlfw.GetMonitorPos(m, out var mx, out var my);
        var mode = LockedGlfw.GetVideoMode(m);
        if (mode == null) return;

        var logicalW = mode->Width;
        var logicalH = mode->Height;

        // Workaround: +1 px width physical
        var physicalW = logicalW + 1;
        var physicalH = logicalH;

        LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, false);
        LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, false);

        LockedGlfw.SetWindowPos(win, mx, my);
        LockedGlfw.SetWindowSize(win, physicalW, physicalH);

        LockedGlfw.FocusWindow(win);
    }

    private void ExitFullscreenIfNeeded(WindowHandle* win, int windowId)
    {
        if (!_fullscreenRestoreRects.TryGetValue(windowId, out var rr) || !rr.valid)
            return;

        LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, true);
        LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, true);

        LockedGlfw.SetWindowPos(win, rr.x, rr.y);
        LockedGlfw.SetWindowSize(win, rr.w, rr.h);

        _fullscreenRestoreRects.TryRemove(windowId, out _);
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

#region Windows only (ClickThrough)

    private static void TryApplyClickThrough_HostThreadUnsafe(WindowHandle* win, bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;

        // We need HWND; if your Silk.NET doesn't expose glfwGetWin32Window,
        // use the DllImport fallback.
        var hwnd = Win32ClickThrough.GetHwndOrZero(win);
        if (hwnd == nint.Zero) return;

        Win32ClickThrough.SetClickThrough(hwnd, enabled);
    }

    private static partial class Win32ClickThrough
    {
        [System.Runtime.InteropServices.LibraryImport("glfw3.dll", EntryPoint = "glfwGetWin32Window")]
        private static partial nint glfwGetWin32Window(WindowHandle* window);

        public static nint GetHwndOrZero(WindowHandle* win)
        {
            try { return glfwGetWin32Window(win); }
            catch { return nint.Zero; }
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

        [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

        public static void SetClickThrough(nint hwnd, bool enabled)
        {
            var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            if (enabled)
            {
                ex |= WS_EX_LAYERED;
                ex |= WS_EX_TRANSPARENT;
            }
            else
            {
                ex &= ~WS_EX_TRANSPARENT;
                // keep LAYERED if you want other layered uses; otherwise clear it too:
                // ex &= ~WS_EX_LAYERED;
            }

            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new nint(ex));
        }
    }

#endregion
}