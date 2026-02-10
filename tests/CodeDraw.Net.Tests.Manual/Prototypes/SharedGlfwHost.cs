using System.Collections.Concurrent;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe partial class SharedGlfwHost : IDisposable
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
    private readonly ConcurrentDictionary<int, (int x, int y, int w, int h, bool valid)> _fullscreenRestoreRects = new();

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
            WindowSize = (w, wpx, hpx) => Enq(new WindowSizeEvent(id, wpx, hpx)),
            FramebufferSize = (w, wpx, hpx) => Enq(new FramebufferSizeEvent(id, wpx, hpx)),
        };

        _callbacks[(nint)win] = cbs;

        Glfw.SetInputMode(win, (StickyAttributes)0x00033004, true); //capslock support (https://www.glfw.org/docs/3.3/glfw3_8h.html#a07b84de0b52143e1958f88a7d9105947)
        _glfw!.SetCursorPosCallback(win, cbs.CursorPos);
        _glfw.SetMouseButtonCallback(win, cbs.MouseButton);
        _glfw.SetScrollCallback(win, cbs.Scroll);
        _glfw.SetKeyCallback(win, cbs.Key);
        _glfw.SetCharCallback(win, cbs.Char);
        _glfw.SetWindowCloseCallback(win, cbs.Close);
        _glfw.SetWindowPosCallback(win, cbs.WindowPos);
        _glfw.SetWindowSizeCallback(win, cbs.WindowSize);
        _glfw.SetFramebufferSizeCallback(win, cbs.FramebufferSize);

        return;

        void Enq(object e)
        {
            if (_inputQueues.TryGetValue(id, out var q))
                q.Enqueue(e);
        }
    }

    internal void ApplyWindowSettingsAsync(
        WindowHandle* win,
        int windowId,
        WindowSettingsSnapshot desired,
        WindowDirty dirty,
        WindowSettingsHandle settings)
    {
        if (win == null) return;

        InvokeHostAsync(() =>
        {
            if (!IsWindowAlive(win)) return;

            var glfw = _glfw!;
            var l = GetWindowLock(win);

            lock (l)
            {
                // Title
                if ((dirty & WindowDirty.Title) != 0)
                    glfw.SetWindowTitle(win, desired.Title ?? "");

                // Position
                if ((dirty & WindowDirty.WindowPos) != 0)
                    glfw.SetWindowPos(win, desired.WindowPosition.X, desired.WindowPosition.Y);

                // AlwaysOnTop
                if ((dirty & WindowDirty.AlwaysOnTop) != 0)
                    glfw.SetWindowAttrib(win, WindowAttributeSetter.Floating, desired.AlwaysOnTop);

                // Border
                if ((dirty & WindowDirty.Border) != 0)
                {
                    ApplyFrameAndResizability_HostThreadUnsafe(glfw, win, desired);
                    ApplyConstraintsIfWindowed_HostThreadUnsafe(glfw, win, desired);
                }

                // Size
                if ((dirty & WindowDirty.CanvasSize) != 0)
                    glfw.SetWindowSize(win, desired.Size.X, desired.Size.Y);

                //TODO: check if jumping back form fullscreen to normal while a limit is active now, will apply that limit

                // State (ignore Fullscreen for now)
                if ((dirty & WindowDirty.WindowState) != 0)
                    ApplyWindowState_HostThreadUnsafe(glfw, win, windowId, desired, settings);

                // ClickThrough
                if ((dirty & WindowDirty.ClickThrough) != 0)
                    TryApplyClickThrough_HostThreadUnsafe(glfw, win, desired.ClickThrough);

                settings.MarkApplied(dirty & ~(WindowDirty.WindowPos | WindowDirty.CanvasSize));
                settings.SyncCurrentFromHost(desired, dirty);
            }
        });
    }

    private void ApplyFrameAndResizability_HostThreadUnsafe(Glfw glfw, WindowHandle* win, WindowSettingsSnapshot d)
    {
        // Decorations
        glfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, d.FrameMode == WindowFrameMode.Decorated);

        // Resizable flag only depends on resize mode (not frame)
        var resizable = d.ResizeMode != WindowResizeMode.Fixed;
        glfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, resizable);
    }

    private void ClearAllConstraints_HostThreadUnsafe(Glfw glfw, WindowHandle* win)
    {
        glfw.SetWindowSizeLimits(win, Glfw.DontCare, Glfw.DontCare, Glfw.DontCare, Glfw.DontCare);
        glfw.SetWindowAspectRatio(win, Glfw.DontCare, Glfw.DontCare);
    }

    private void ApplyConstraintsIfWindowed_HostThreadUnsafe(Glfw glfw, WindowHandle* win, WindowSettingsSnapshot d)
    {
        ClearAllConstraints_HostThreadUnsafe(glfw, win);

        if (d.State != WindowState.Windowed)
            return;

        switch (d.ResizeMode)
        {
            case WindowResizeMode.Limited:
                glfw.SetWindowSizeLimits(win, d.MinSize.X, d.MinSize.Y, d.MaxSize.X, d.MaxSize.Y);
                break;

            case WindowResizeMode.Aspect:
                glfw.SetWindowAspectRatio(win, d.AspectRatio.X, d.AspectRatio.Y);
                break;

            default:
                // Resizable/Fixed => no constraints
                break;
        }
    }

    private void ApplyWindowState_HostThreadUnsafe(Glfw glfw, WindowHandle* win, int windowId, WindowSettingsSnapshot desired, WindowSettingsHandle settings)
    {
        switch (desired.State)
        {
            case WindowState.Windowed:
            {
                // If we were fullscreen, restore from monitor mode back to windowed rect
                ExitFullscreenIfNeeded(glfw, win, windowId);
                glfw.RestoreWindow(win);
                break;
            }

            case WindowState.Minimized:
            {
                ExitFullscreenIfNeeded(glfw, win, windowId);
                glfw.IconifyWindow(win);
                break;
            }

            case WindowState.Maximized:
            {
                ExitFullscreenIfNeeded(glfw, win, windowId);

                if (desired.ResizeMode is not WindowResizeMode.Fixed)
                    Console.WriteLine($"[Host] INVALID maximize attempt for ResizeMode {desired.ResizeMode}, applying normal maximize.");

                // Windows quirk you found: when you remove resize border (Fixed) maximize can cover taskbar.
                // Workaround: for Fixed/Hidden, do "workarea maximize" manually.
                if (desired.FrameMode is WindowFrameMode.Decorated)
                {
                    var mi = FindBestMonitorIndexForWindow_HostThreadUnsafe(win);
                    var mons = GetMonitorsInternal_HostThreadUnsafe(glfw);
                    if (mons.Count == 0) { glfw.MaximizeWindow(win); return; } //TODO: unsafe fallback. use EnterFullscreen instead
                    var m = mons[Math.Clamp(mi, 0, mons.Count - 1)];
                    glfw.SetWindowPos(win, m.WorkX, m.WorkY);
                    glfw.SetWindowSize(win, m.WorkWidth, m.WorkHeight);
                    break;
                }

                {
                    // we need to move it up by the height of the topbar and increase its height by that much.
                    var mi = FindBestMonitorIndexForWindow_HostThreadUnsafe(win);
                    var mons = GetMonitorsInternal_HostThreadUnsafe(glfw);
                    if (mons.Count == 0) { glfw.MaximizeWindow(win); return; }  //TODO: unsafe fallback. use EnterFullscreen instead
                    var m = mons[Math.Clamp(mi, 0, mons.Count - 1)];
                    const int offset = 20; // idk yet where to get the actual height.
                    glfw.SetWindowPos(win, m.WorkX, m.WorkY - offset);
                    glfw.SetWindowSize(win, m.WorkWidth, m.WorkHeight + offset);
                }

                break;
            }

            case WindowState.Fullscreen:
            {
                EnterFullscreen(glfw, win, windowId, settings);
                break;
            }
        }
    }

    private void ApplyMaximizeWorkarea_HostThreadUnsafe(Glfw glfw, WindowHandle* win)
    {
        var mi = FindBestMonitorIndexForWindow_HostThreadUnsafe(win);
        var mons = GetMonitorsInternal_HostThreadUnsafe(glfw);
        if (mons.Count == 0) { glfw.MaximizeWindow(win); return; }
        var m = mons[Math.Clamp(mi, 0, mons.Count - 1)];
        glfw.SetWindowPos(win, m.WorkX, m.WorkY);
        glfw.SetWindowSize(win, m.WorkWidth, m.WorkHeight);
    }

    private void EnterFullscreen(Glfw glfw, WindowHandle* win, int windowId, WindowSettingsHandle settings)
    {
        // save restore rect once
        if (!_fullscreenRestoreRects.TryGetValue(windowId, out var rr) || !rr.valid)
        {
            glfw.GetWindowPos(win, out var x, out var y);
            glfw.GetWindowSize(win, out var w, out var h);
            _fullscreenRestoreRects[windowId] = (x, y, w, h, true);
        }

        // clear constraints
        glfw.SetWindowSizeLimits(win, Glfw.DontCare, Glfw.DontCare, Glfw.DontCare, Glfw.DontCare);
        glfw.SetWindowAspectRatio(win, Glfw.DontCare, Glfw.DontCare);

        var mi = FindBestMonitorIndexForWindow_HostThreadUnsafe(win);
        var monitors = glfw.GetMonitors(out var count);
        if (count <= 0 || monitors == null) return;
        mi = Math.Clamp(mi, 0, count - 1);
        var m = monitors[mi];

        glfw.GetMonitorPos(m, out var mx, out var my);
        var mode = glfw.GetVideoMode(m);
        if (mode == null) return;

        var logicalW = mode->Width;
        var logicalH = mode->Height;

        // Workaround: +1 px width physical
        var physicalW = logicalW + 1;
        var physicalH = logicalH;

        glfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, false);
        glfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, false);

        glfw.SetWindowPos(win, mx, my);
        glfw.SetWindowSize(win, physicalW, physicalH);

        // Lie to user: update settings size to logical
        settings.SyncSizeFromHost(new Vector2<int>(logicalW, logicalH));

        glfw.FocusWindow(win);
    }

    private void ExitFullscreenIfNeeded(Glfw glfw, WindowHandle* win, int windowId)
    {
        if (!_fullscreenRestoreRects.TryGetValue(windowId, out var rr) || !rr.valid)
            return;

        glfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, true);
        glfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, true);

        glfw.SetWindowPos(win, rr.x, rr.y);
        glfw.SetWindowSize(win, rr.w, rr.h);

        _fullscreenRestoreRects.TryRemove(windowId, out _);
    }

#region Windows only (ClickThrough)

    private static void TryApplyClickThrough_HostThreadUnsafe(Glfw glfw, WindowHandle* win, bool enabled)
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