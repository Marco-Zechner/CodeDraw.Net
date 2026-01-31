using System.Collections.Concurrent;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe class SharedGlfwHost : IDisposable
{
    public readonly record struct MouseMoveEvent(int WindowId, double X, double Y)
    {
        public override string ToString() => $"MouseMoveEvent(WindowId={WindowId}, X={X:0.0}, Y={Y:0.0})";
    }

    public readonly record struct MouseButtonEvent(
        int WindowId,
        MouseButton Button,
        InputAction Action,
        KeyModifiers Mods)
    {
        public override string ToString() => $"MouseButtonEvent(WindowId={WindowId}, Button={Button}, Action={Action}, Mods={Mods})";
    }

    public readonly record struct MouseWheelEvent(int WindowId, double Dx, double Dy)
    {
        public override string ToString() => $"MouseWheelEvent(WindowId={WindowId}, Dx={Dx:0.00}, Dy={Dy:0.00})";
    }

    public readonly record struct KeyEvent(int WindowId, Keys Key, int Scancode, InputAction Action, KeyModifiers Mods)
    {
        public override string ToString() => $"KeyEvent(WindowId={WindowId}, Key={Key}, Scancode={Scancode}, Action={Action}, Mods={Mods})";
    }

    public readonly record struct CharEvent(int WindowId, uint Codepoint)
    {
        public override string ToString()
        {
            var c = char.ConvertFromUtf32((int)Codepoint);
            return $"CharEvent(WindowId={WindowId}, Codepoint=U+{Codepoint:X4} ('{c}'))";
        }
    }

    public sealed class InputHub
    {
        private readonly ConcurrentQueue<object> _q = new();

        public event Action<MouseMoveEvent>? MouseMove;
        public event Action<MouseButtonEvent>? MouseButtonDown;
        public event Action<MouseButtonEvent>? MouseButtonUp;
        public event Action<MouseButtonEvent>? MouseButton;
        public event Action<MouseWheelEvent>? MouseWheel;
        public event Action<KeyEvent>? KeyDown;
        public event Action<KeyEvent>? KeyUp;
        public event Action<KeyEvent>? Key;
        public event Action<CharEvent>? Char;

        internal void Enqueue(object evt) => _q.Enqueue(evt);

        /// Call this from your main loop / any thread you want to “own” input dispatch.
        public void Pump(int max = 10_000)
        {
            var n = 0;
            while (n++ < max && _q.TryDequeue(out var e))
            {
                switch (e)
                {
                    case MouseMoveEvent mm: MouseMove?.Invoke(mm); break;
                    case MouseButtonEvent mb:
                        switch (mb.Action)
                        {
                            case InputAction.Press: MouseButtonDown?.Invoke(mb); break;
                            case InputAction.Release: MouseButtonUp?.Invoke(mb); break;
                            case InputAction.Repeat: // does not happen for mouse buttons
                            default: MouseButton?.Invoke(mb); break;
                        }
                        break;
                    case MouseWheelEvent mw: MouseWheel?.Invoke(mw); break;
                    case KeyEvent ke:
                        switch (ke.Action)
                        {
                            case InputAction.Press: KeyDown?.Invoke(ke); break;
                            case InputAction.Release: KeyUp?.Invoke(ke); break;
                            case InputAction.Repeat:
                            default: Key?.Invoke(ke); break;
                        }
                        break;
                    case CharEvent ce: Char?.Invoke(ce); break;
                }
            }
        }
    }

    public InputHub Input { get; } = new();

    private int _nextWindowId;
    private readonly ConcurrentDictionary<nint, int> _winToId = new(); // key: (nint)WindowHandle*


    public readonly record struct MonitorInfo(
        nint GlfwHandle,         // monitor*
        string Name,
        int X, int Y,            // virtual desktop coords
        int Width, int Height,   // work area or video mode
        float ContentScaleX,
        float ContentScaleY,
        int RefreshRate
    );

    public readonly record struct WindowPlacement(
        int X, int Y,
        int Width, int Height,
        bool BorderlessFullscreen,
        int MonitorIndex // for fullscreen
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
                var mons = GetMonitorsSafe();
                var mi = mons[placement.MonitorIndex];

                _glfw!.WindowHint(WindowHintBool.Decorated, false);
                _glfw.WindowHint(WindowHintBool.Resizable, false);

                // Use monitor resolution if width/height are <=0
                var w = placement.Width  > 0 ? placement.Width  : mi.Width;
                var h = placement.Height > 0 ? placement.Height : mi.Height;

                result = _glfw.CreateWindow(w, h, title, null, _shareRoot);
                if (result == null) throw new Exception("CreateWindow failed");

                _glfw.SetWindowPos(result, mi.X, mi.Y);
            }
            else
            {
                result = _glfw!.CreateWindow(placement.Width, placement.Height, title, null, _shareRoot);
                if (result != null)
                {
                    _glfw.SetWindowPos(result, placement.X, placement.Y);
                }
            }

            if (result == null) throw new Exception("CreateWindow failed");
            var id = Interlocked.Increment(ref _nextWindowId);
            _winToId[(nint)result] = id;

            RegisterInputCallbacks(result, id);

            _glfw.MakeContextCurrent(result);
            _glfw.MakeContextCurrent(null);

            done.Set();
        });

        done.WaitOne();
        return result;
    }

    /// Creates a hidden window with its own context, in the share-group.
    public WindowHandle* CreateHiddenWindow(int w, int h, string title = "hidden")
    {
        WindowHandle* result = null;
        using var done = new AutoResetEvent(false);

        EnqueueUi(() =>
        {
            ApplyCommonHints(_glfw!);
            // Hidden by default
            _glfw!.WindowHint(WindowHintBool.Visible, false);

            result = _glfw.CreateWindow(w, h, title, null, _shareRoot);
            if (result == null) throw new Exception("CreateHiddenWindow failed");

            _glfw.HideWindow(result);

            // Bind once for stability
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

        // keep default visible=true unless caller overrides
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

            monitors.Add(new MonitorInfo(
                (nint)mPtr,
                name,
                mx, my,
                width, height,
                scaleX, scaleY,
                refreshRate
            ));
        }
        return monitors;
    }

    private void RegisterInputCallbacks(WindowHandle* win, int id)
    {
        // Mouse move
        _glfw!.SetCursorPosCallback(win, (w, x, y) =>
        {
            Input.Enqueue(new MouseMoveEvent(id, x, y));
        });

        // Mouse buttons
        _glfw.SetMouseButtonCallback(win, (w, button, action, mods) =>
        {
            Input.Enqueue(new MouseButtonEvent(id, button, action, mods));
        });

        // Scroll
        _glfw.SetScrollCallback(win, (w, dx, dy) =>
        {
            Input.Enqueue(new MouseWheelEvent(id, dx, dy));
        });

        // Keys
        _glfw.SetKeyCallback(win, (w, key, scancode, action, mods) =>
        {
            Input.Enqueue(new KeyEvent(id, key, scancode, action, mods));
        });

        // Text input
        _glfw.SetCharCallback(win, (w, codepoint) =>
        {
            Input.Enqueue(new CharEvent(id, codepoint));
        });
    }
}
