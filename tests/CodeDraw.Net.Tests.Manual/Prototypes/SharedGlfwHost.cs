using System.Collections.Concurrent;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe class SharedGlfwHost : IDisposable
{
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
}
