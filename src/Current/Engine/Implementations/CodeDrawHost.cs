// Engine/CodeDrawHost.cs
using System;
using System.Collections.Concurrent;
using System.Threading;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Engine;

internal unsafe sealed class CodeDrawHost : IDisposable
{
    public static CodeDrawHost Instance { get; } = new();

    public Glfw Glfw => _glfw!;
    public WindowHandle* ShareRoot => _shareRoot;

    public DateTime StartTimeUtc { get; private set; }

    private Thread? _uiThread;
    private Glfw? _glfw;
    private volatile bool _running;
    private readonly AutoResetEvent _started = new(false);
    private readonly AutoResetEvent _work = new(false);
    private readonly ConcurrentQueue<Action> _uiJobs = new();

    private WindowHandle* _shareRoot = null;

    private CodeDrawHost() { }

    public void EnsureStarted()
    {
        if (_running) return;
        _running = true;
        _uiThread = new Thread(UIThreadMain) { IsBackground = true, Name = "CodeDraw-GLFW-UI" };
        _uiThread.Start();
        _started.WaitOne(); // wait until GLFW + share root created
    }

    public void Dispose() => Stop();

    public void Stop()
    {
        if (!_running) return;
        EnqueueUI(() => _running = false);
        _uiThread?.Join();
        _uiThread = null;
    }

    /// <summary>Enqueue a job to be executed on the UI/GLFW thread (fire-and-forget).</summary>
    public void EnqueueUI(Action job)
    {
        _uiJobs.Enqueue(job);
        _work.Set();
    }

    /// <summary>Execute a job on the UI/GLFW thread and wait for completion.</summary>
    public void EnqueueUISync(Action job)
    {
        var done = new AutoResetEvent(false);
        Exception? ex = null;

        EnqueueUI(() =>
        {
            try { job(); }
            catch (Exception e) { ex = e; }
            finally { done.Set(); }
        });

        done.WaitOne();
        if (ex != null) throw ex;
    }

    /// <summary>Create a visible window in the share group (runs on UI thread).</summary>
    public WindowHandle* CreateWindow(int w, int h, string title)
    {
        WindowHandle* result = null;
        EnqueueUISync(() =>
        {
            ApplyCommonHints(_glfw!);
            result = _glfw!.CreateWindow(w, h, title, null, _shareRoot);
            if (result == null) throw new Exception("CreateWindow failed");

            // Bind once for stability, then unbind (esp. on Windows)
            _glfw.MakeContextCurrent(result);
            _glfw.MakeContextCurrent(null);
        });
        return result!;
    }

    /// <summary>Request destruction of a window on the UI thread.</summary>
    public void DestroyWindow(WindowHandle* win)
    {
        if (win == null) return;
        EnqueueUI(() =>
        {
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
        if (_shareRoot == null) throw new Exception("Failed to create share-root");
        _glfw.HideWindow(_shareRoot);
        _glfw.MakeContextCurrent(_shareRoot);
        // Create a GL instance once here (optional). We don’t keep it; render threads get their own.
        var gl = GL.GetApi(_glfw.GetProcAddress);
        _glfw.MakeContextCurrent(null);

        StartTimeUtc = DateTime.UtcNow;
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
        // GL 3.3 core
        glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

        // Pixel format consistent across share group
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
        glfw.WindowHint(WindowHintBool.TransparentFramebuffer, true);
    }
}
