// SharedGlfwHost.cs
using System;
using System.Collections.Concurrent;
using System.Threading;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Helpers;

public unsafe sealed class SharedGlfwHost : IDisposable
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
        _uiThread = new Thread(UIThreadMain) { IsBackground = true, Name = "GLFW-UI" };
        _uiThread.Start();
        _started.WaitOne(); // wait until GLFW + share root created
    }

    public void Dispose() => Stop();

    public void Stop()
    {
        if (!_running) return;
        // enqueue stop on UI thread
        EnqueueUI(() => _running = false);
        _uiThread?.Join();
        _uiThread = null;
    }

    /// Enqueue a job to be executed on the UI thread (GLFW thread).
    public void EnqueueUI(Action job)
    {
        _uiJobs.Enqueue(job);
        _work.Set();
    }

    /// Create a visible window in the share group (runs on UI thread).
    public WindowHandle* CreateWindow(int w, int h, string title)
    {
        WindowHandle* result = null;
        var done = new AutoResetEvent(false);

        EnqueueUI(() =>
        {
            // All hints should match share root’s pixel format & version
            ApplyCommonHints(_glfw!);
            result = _glfw!.CreateWindow(w, h, title, null, _shareRoot);
            if (result == null) throw new Exception("CreateWindow failed");

            // Bind once for Windows stability, then unbind
            _glfw.MakeContextCurrent(result);
            _glfw.MakeContextCurrent(null);

            // Track close events independently
            _glfw.SetWindowCloseCallback(result, (win) =>
            {
                // Mark should-close; GLFW will also set its internal flag.
                // We do nothing else here; the render thread will see it.
            });

            done.Set();
        });

        done.WaitOne();
        return result;
    }

    /// Request to destroy a window on the UI thread.
    public void DestroyWindow(WindowHandle* win)
    {
        if (win == null) return;
        EnqueueUI(() =>
        {
            // Ensure its context is not current on any thread
            _glfw!.MakeContextCurrent(null);
            _glfw.DestroyWindow(win);
        });
    }

    private void UIThreadMain()
    {
        _glfw = Glfw.GetApi();
        if (!_glfw.Init()) throw new Exception("GLFW init failed");

        // Shared hidden root
        ApplyCommonHints(_glfw);
        _shareRoot = _glfw.CreateWindow(1, 1, "share-root", null, null);
        if (_shareRoot == null) throw new Exception("Failed to create share root");
        _glfw.HideWindow(_shareRoot);
        _glfw.MakeContextCurrent(_shareRoot);
        var gl = GL.GetApi(_glfw.GetProcAddress);
        _glfw.MakeContextCurrent(null);

        _started.Set();

        // UI loop
        while (_running)
        {
            // process enqueued jobs
            while (_uiJobs.TryDequeue(out var j))
            {
                try { j(); } catch (Exception ex) { Console.WriteLine($"[UI job error] {ex}"); }
            }

            _glfw.PollEvents();

            // Wait a bit or until work arrives
            _work.WaitOne(1);
        }

        // Drain pending jobs (e.g., destroys)
        while (_uiJobs.TryDequeue(out var j))
        {
            try { j(); } catch (Exception ex) { Console.WriteLine($"[UI drain error] {ex}"); }
        }

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
        glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

        // Pixel format: match across share group
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
    }
}
