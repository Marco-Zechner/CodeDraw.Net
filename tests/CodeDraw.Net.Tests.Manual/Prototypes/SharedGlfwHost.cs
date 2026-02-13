using System.Collections.Concurrent;
using System.Diagnostics;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe class SharedGlfwHost : IDisposable
{
    private sealed class LayerRefInfo
    {
        public int RefCount;
        public bool IsAuto;
    }
    
    private int _nextWindowId;
    private readonly ConcurrentDictionary<int, nint> _idToWin = new();
    private readonly ConcurrentDictionary<nint, int> _winToId = new();
    private readonly ConcurrentDictionary<int, ConcurrentQueue<object>> _inputQueues = new();
    private readonly ConcurrentDictionary<int, WeakReference<CodeDrawWindow>> _idToObj = new();
    private readonly ConcurrentDictionary<int, CodeDrawLayer?> _windowToLayer = new();
    private readonly ConcurrentDictionary<CodeDrawLayer, LayerRefInfo> _layerRefs = new();
    
    public int ReserveWindowId()
    {
        var id = Interlocked.Increment(ref _nextWindowId);
        _inputQueues[id] = new ConcurrentQueue<object>();
        EnsureHostQueue(id);
        return id;
    }
    
    public void ReleaseWindowId(int windowId)
    {
        // Called from CodeDrawWindow.Dispose (final kill)
        InvokeHostSync(() =>
        {
            DestroyWindowById(windowId);

            _inputQueues.TryRemove(windowId, out _);
            _idToObj.TryRemove(windowId, out _);
        });
    }
    
    internal bool IsWindowAliveById(int windowId)
    {
        if (!_idToWin.TryGetValue(windowId, out var w) || (WindowHandle*)w == null) return false;
        return IsWindowAlive((WindowHandle*)w);
    }

    internal void RegisterWindowObject(WindowHandle* win, int windowId, CodeDrawWindow obj)
    {
        if (win == null) return;
        _idToObj[windowId] = new WeakReference<CodeDrawWindow>(obj);
    }
    
    internal CodeDrawWindow? TryGetWindowObject(int windowId)
    {
        if (!_idToObj.TryGetValue(windowId, out var wr)) return null;
        return wr.TryGetTarget(out var w) ? w : null;
    }
    
    public HostInputHub Input { get; } = new();

    internal abstract record HostInputEvent(int WindowId);

    private sealed record HostKeyEvent(int WindowId, Keys Key, int Scancode, InputAction Action, ModifierKeys Mods)
        : HostInputEvent(WindowId);

    private sealed record HostMouseButtonEvent(int WindowId, MouseButton Button, InputAction Action, ModifierKeys Mods)
        : HostInputEvent(WindowId);

    private sealed record HostScrollEvent(int WindowId, double Dx, double Dy)
        : HostInputEvent(WindowId);

    private sealed record HostCursorPosEvent(int WindowId, double X, double Y)
        : HostInputEvent(WindowId);

    private readonly ConcurrentDictionary<int, ConcurrentQueue<HostInputEvent>> _hostInputById = new();

    private void EnsureHostQueue(int windowId)
        => _hostInputById.TryAdd(windowId, new ConcurrentQueue<HostInputEvent>());

    private void RemoveHostQueue(int windowId)
        => _hostInputById.TryRemove(windowId, out _);

    private void EnqueueHostInput(HostInputEvent e)
    {
        if (_hostInputById.TryGetValue(e.WindowId, out var q))
            q.Enqueue(e);
    }

    public void PumpHostInputForWindow(CodeDrawWindow windowObj, int max = 10_000)
    {
        if (windowObj.IsDisposed) return;
        var id = windowObj.WindowId;

        if (!_hostInputById.TryGetValue(id, out var q)) return;

        var n = 0;
        while (n++ < max && q.TryDequeue(out var e))
            Input.Dispatch(windowObj, e);
    }
    
    public sealed class HostInputHub
    {
        public event Action<CodeDrawWindow, Keys, ModifierKeys>? OnKeyDown;
        public event Action<CodeDrawWindow, Keys, ModifierKeys>? OnKeyUp;
        public event Action<CodeDrawWindow, Keys, ModifierKeys>? OnKeyRepeat;

        public event Action<CodeDrawWindow, MouseButton, ModifierKeys>? OnMouseDown;
        public event Action<CodeDrawWindow, MouseButton, ModifierKeys>? OnMouseUp;

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
                    if (mb.Action == InputAction.Press) OnMouseDown?.Invoke(win, mb.Button, mb.Mods);
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
    
    public void DestroyWindowById(int windowId)
    {
        InvokeHostSync(() =>
        {
            if (_idToWin.TryRemove(windowId, out var ptr) && (WindowHandle*)ptr != null)
            {
                DestroyWindowInternal((WindowHandle*)ptr, windowId);
            }
        });
    }
    
    private void DestroyWindowInternal(WindowHandle* win, int windowId)
    {
        _callbacks.TryRemove((nint)win, out _);

        RemoveHostQueue(windowId);

        _winToId.TryRemove((nint)win, out _);
        _idToWin.TryRemove(windowId, out _);

        LockedGlfw.MakeContextCurrent(null);
        LockedGlfw.DestroyWindow(win);
        
        OnNativeWindowDestroyed();
    }
    
    internal bool IsWindowAlive(WindowHandle* win)
        => win != null && _winToId.ContainsKey((nint)win);

    internal void DrainWindowInput(int windowId, Action<object> handle, int max = 50_000)
    {
        if (!_inputQueues.TryGetValue(windowId, out var q)) return;
        var n = 0;
        
        while (n++ < max && q.TryDequeue(out var evt))
            handle(evt);
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

    private void RegisterInputCallbacks(WindowHandle* win, int id)
    {
        var cbs = new WindowCallbacks
        {
            CursorPos = (w, x, y) =>
            {
                Enq(new MouseMoveEvent(id, x, y));
                EnqueueHostInput(new HostCursorPosEvent(id, x, y));
            },
            MouseButton = (w, button, action, mods) =>
            {
                var keyMods = (ModifierKeys)mods;
                Enq(new MouseButtonEvent(id, button, action, keyMods));
                EnqueueHostInput(new HostMouseButtonEvent(id, button, action, keyMods));
            },
            Scroll = (w, dx, dy) =>
            {
                Enq(new MouseWheelEvent(id, dx, dy));
                EnqueueHostInput(new HostScrollEvent(id, dx, dy));
            },
            Key = (w, key, scancode, action, mods) =>
            {
                var keyMods = (ModifierKeys)mods;
                Enq(new KeyEvent(id, key, scancode, action, keyMods));
                EnqueueHostInput(new HostKeyEvent(id, key, scancode, action, keyMods));
            },
            Char = (w, codepoint) => Enq(new CharEvent(id, codepoint)),
            Close = (w) => Enq(new WindowCloseRequestedEvent(id)),
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
            Maximize = (w, maximized) => Enq(new WindowMaximizedEvent(id, maximized)),
            Iconify = (w, iconified) => Enq(new WindowIconifiedEvent(id, iconified)),
        };

        _callbacks[(nint)win] = cbs;

        LockedGlfw.SetInputMode(win, (StickyAttributes)0x00033004, true);
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

    private readonly WindowStateMachine _stateMachine = new();
    private readonly ConcurrentDictionary<int, long> _lastResizeTick = new();
    private readonly ConcurrentDictionary<int, int> _isLiveResize = new(); // 0/1

    private int _aliveWindows; // number of native windows currently alive
    private readonly ManualResetEventSlim _allClosed = new(initialState: true);
    private long _allClosedSinceTicks;
    
    private List<MonitorInfo> _monitorsCache = [];
    private int _monitorsDirty = 1; // start dirty so we build once

    private GlfwCallbacks.MonitorCallback? _monitorCallback; // keep delegate alive
    
    private static long NowTicks() => Stopwatch.GetTimestamp();
    private static double TicksToMs(long dt) => dt * 1000.0 / Stopwatch.Frequency;

    private SharedGlfwHost() { }
    
    private void OnNativeWindowCreated()
    {
        var n = Interlocked.Increment(ref _aliveWindows);
        if (n == 1)
            _allClosed.Reset();
    }

    private void OnNativeWindowDestroyed()
    {
        var n = Interlocked.Decrement(ref _aliveWindows);
        if (n > 0) return;

        Interlocked.Exchange(ref _aliveWindows, 0);
        _allClosedSinceTicks = Stopwatch.GetTimestamp();
        _allClosed.Set();
    }
    
    public void WaitUntilAllWindowsClosed(int stableMs = 0, CancellationToken ct = default)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            _allClosed.Wait(ct); // blocks with basically no CPU

            if (stableMs <= 0)
                return;

            // Optional “stability window”: handle close->reopen flicker.
            var since = Volatile.Read(ref _allClosedSinceTicks);
            var elapsedMs = (Stopwatch.GetTimestamp() - since) * 1000.0 / Stopwatch.Frequency;

            if (elapsedMs >= stableMs && Volatile.Read(ref _aliveWindows) == 0)
                return;

            // If we woke up but stability not met, wait a bit WITHOUT spinning hard:
            Thread.Sleep(Math.Min(5, stableMs));
        }
    }
    
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

    internal int GetWindowId(WindowHandle* win)
    {
        if (win == null) return 0;
        return _winToId.TryGetValue((nint)win, out var id) ? id : 0;
    }

    internal WindowHandle* CreateHiddenLayerWindow(int w, int h, string title)
    {
        WindowHandle* win = null;
        InvokeHostSync(() =>
        {
            ApplyCommonHints();

            LockedGlfw.WindowHint(WindowHintBool.Visible, false);
            LockedGlfw.WindowHint(WindowHintBool.Decorated, false);
            LockedGlfw.WindowHint(WindowHintBool.Resizable, false);
            LockedGlfw.WindowHint(WindowHintBool.Focused, false);

            win = LockedGlfw.CreateWindow(w, h, title, null, ShareRoot);
            ApplyCommonHints();
            if (win == null) throw new Exception("CreateHiddenLayerWindow failed");
        });
        return win;
    }
    
    internal void DestroyHiddenLayerWindow(WindowHandle* win)
    {
        if (win == null) return;
        InvokeHostSync(() =>
        {
            if (!IsWindowAlive(win)) { LockedGlfw.DestroyWindow(win); return; }
            var id = GetWindowId(win);
            DestroyWindowInternal(win, id);
        });
    }
    
    internal WindowHandle* CreateOrRecreateWindowForId(
        int windowId,
        int x, int y,
        int w, int h,
        string title,
        CodeDrawWindow owner,
        bool stealFocusOnOpen)
    {
        WindowHandle* result = null;

        InvokeHostSync(() =>
        {
            // destroy existing native window for that id if any
            if (_idToWin.TryGetValue(windowId, out var oldPtr) && (WindowHandle*)oldPtr != null)
            {
                DestroyWindowInternal((WindowHandle*)oldPtr, windowId);
                _idToWin.TryRemove(windowId, out _);
            }

            ApplyCommonHints();

            WithCreateHints(stealFocusOnOpen, () =>
            {
                result = LockedGlfw.CreateWindow(w, h, title, null, ShareRoot);
            });
            if (result == null) throw new Exception("CreateWindow failed");

            OnNativeWindowCreated();
            
            LockedGlfw.SetWindowPos(result, x, y);

            // mappings
            _idToWin[windowId] = (nint)result;
            _winToId[(nint)result] = windowId;

            EnsureHostQueue(windowId);
            RegisterWindowObject(result, windowId, owner);

            RegisterInputCallbacks(result, windowId);

            LockedGlfw.MakeContextCurrent(result);
            LockedGlfw.MakeContextCurrent(null);
            
            if (stealFocusOnOpen)
                LockedGlfw.FocusWindow(result);
        });

        return result;
    }
    
    internal void RegisterAutoLayerOwner(int windowId, CodeDrawLayer layer)
    {
        // mark layer as auto (even if shared later)
        var info = _layerRefs.GetOrAdd(layer, _ => new LayerRefInfo());
        lock (info)
        {
            info.IsAuto = true;
        }

        // Attach to this window as current layer (increments refcount)
        AssignWindowLayer(windowId, layer);
    }
    
    internal void AssignWindowLayer(int windowId, CodeDrawLayer? newLayer)
    {
        CodeDrawLayer? oldLayer = null;

        // swap mapping window->layer
        _windowToLayer.AddOrUpdate(
            windowId,
            _ => { oldLayer = null; return newLayer; },
            (_, prev) => { oldLayer = prev; return newLayer; });

        if (ReferenceEquals(oldLayer, newLayer)) return;

        if (oldLayer != null) DecrementLayerRef(oldLayer);
        if (newLayer != null) IncrementLayerRef(newLayer);
    }
    
    private void IncrementLayerRef(CodeDrawLayer layer)
    {
        var info = _layerRefs.GetOrAdd(layer, _ => new LayerRefInfo());
        lock (info) { info.RefCount++; }
    }

    private void DecrementLayerRef(CodeDrawLayer layer)
    {
        if (!_layerRefs.TryGetValue(layer, out var info)) return;

        bool dispose = false;
        lock (info)
        {
            info.RefCount--;
            if (info.RefCount <= 0)
            {
                // dispose only if it is an auto layer
                dispose = info.IsAuto;
            }
        }

        if (!dispose) return;

        // Remove entry before dispose to avoid reentrancy weirdness
        _layerRefs.TryRemove(layer, out _);

        try { layer.Dispose(); }
        catch (Exception ex) { Console.WriteLine($"[Host] Auto layer dispose error: {ex}"); }
    }
    
    internal void NotifyWindowDisposed(int windowId)
    {
        if (_windowToLayer.TryRemove(windowId, out var layer) && layer != null)
            DecrementLayerRef(layer);
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
    
    private static void WithCreateHints(bool stealFocusOnOpen, Action body)
    {
        ApplyCommonHints();

        LockedGlfw.WindowHint(WindowHintBool.Focused, stealFocusOnOpen);

        body();
        ApplyCommonHints();
    }
    
    private static bool ShouldRecreateForState(WindowState s)
        => s is WindowState.Maximized
            or WindowState.BorderlessMaximized
            or WindowState.BorderlessFullscreen;

    internal void ApplyWindowSettingsSync(WindowHandle* win, int windowId, WindowSettingsSnapshot desired, WindowDirty dirty)
    {
        // Caller might pass null if closed; allow apply to be deferred.
        if (win == null) return;

        InvokeHostSync(() =>
        {
            // window might have died since call site
            if (!IsWindowAlive(win)) return;

            var needRecreate =
                dirty.HasFlag(WindowDirty.WindowState) &&
                ShouldRecreateForState(desired.State);

            var targetWin = win;

            if (needRecreate)
            {
                // get owner (if any)
                var owner = TryGetWindowObject(windowId);
                if (owner != null)
                {
                    // destroy old
                    DestroyWindowById(windowId);

                    WindowHandle* created = null;
                    WithCreateHints(desired.StealFocusOnOpen, () =>
                    {
                        // create new with current desired snapshot as base
                        created = LockedGlfw.CreateWindow(desired.Size.X, desired.Size.Y, desired.Title, null, ShareRoot);
                    });

                    if (created == null) throw new Exception("CreateWindow failed");
                    
                    LockedGlfw.SetWindowPos(created, desired.WindowPosition.X, desired.WindowPosition.Y);

                    // mappings
                    _idToWin[windowId] = (nint)created;
                    _winToId[(nint)created] = windowId;

                    EnsureHostQueue(windowId);
                    RegisterWindowObject(created, windowId, owner);
                    RegisterInputCallbacks(created, windowId);

                    // touch context once
                    LockedGlfw.MakeContextCurrent(created);
                    LockedGlfw.MakeContextCurrent(null);

                    if (desired.StealFocusOnOpen)
                        LockedGlfw.FocusWindow(created);
                    
                    // tell window object about new handle and restart presenter
                    owner.Host_SetNativeHandle(created);
                    owner.Host_RestartPresenterIfOpen();

                    targetWin = created;

                    // Also: when recreating, you should treat "dirty" as "everything",
                    // because all window attribs are back to defaults.
                    dirty |= WindowDirty.Title | WindowDirty.Border | WindowDirty.WindowPos | WindowDirty.CanvasSize | WindowDirty.AlwaysOnTop | WindowDirty.ClickThrough;
                }
            }

            var monitors = GetMonitorsCached_HostThreadUnsafe();

            _stateMachine.Apply(
                targetWin,
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

    public void NotifyWindowedRect(int windowId, int x, int y, int width, int height)
    {
        InvokeHostAsync(() =>
        {
            _stateMachine.NotifyWindowedRect(windowId, x, y, width, height);
        });
    }
}