using MarcoZechner.CodeDrawDotNet.Engine.Abstractions;
using MarcoZechner.DiagnosticsDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using System.Collections.Concurrent;

namespace MarcoZechner.CodeDrawDotNet.Engine.Impl;

internal sealed unsafe class LayerWorker(Glfw glfw) : IDisposable, ILayerMetricsProvider
{
    private readonly Glfw _glfw = glfw;
    private WindowHandle* _hiddenWin;
    private Thread? _thread;
    private volatile bool _running;
    private readonly AutoResetEvent _wake = new(false);

    private GL? _gl;

    // simple job queue; later you’ll queue “render layer X”, “resize layer Y”, etc.
    private readonly ConcurrentQueue<Action<GL>> _jobs = new();

    private readonly BusyMeter _busy = new(0.25);
    private readonly WorkRate  _work = new();

    // Expose for tests:
    public double BusyPercent => _busy.Duty * 100.0;
    public double JobsPerSec  => _work.JobsPerSec;
    public double IdleSec => _work.IdleSeconds;
    
    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Main) { IsBackground = true, Name = "Layer-Worker" };
        _thread.Start();
    }

    public void Dispose() => Stop();

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _wake.Set();
        _thread?.Join();
        _thread = null;
    }

    public void Enqueue(Action<GL> job)
    {
        _jobs.Enqueue(job);
        _wake.Set();
    }

    private void Main()
    {
        // Create a hidden window that shares with the host’s share-root
        _hiddenWin = _glfw.CreateWindow(1, 1, "layer-root", null, CodeDrawHost.Instance.ShareRoot);
        if (_hiddenWin == null) throw new Exception("layer-root creation failed");
        _glfw.HideWindow(_hiddenWin);
        _glfw.MakeContextCurrent(_hiddenWin);
        _gl = GL.GetApi(_glfw.GetProcAddress);
        _gl.Enable(EnableCap.FramebufferSrgb);
        _glfw.SwapInterval(0); // no vsync on an offscreen target

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // on-demand loop
        while (_running)
        {
            _wake.WaitOne(); // event-driven wake

            while (_running)
            {
                bool didWork = false;

                while (_jobs.TryDequeue(out var j))
                {
                    didWork = true;
                    using (_busy.Scope()) // time only the job work, not the waits
                    {
                        try { j(_gl!); } catch (Exception ex) { Console.WriteLine($"[Layer job error] {ex}"); }
                    }
                    _work.OnJob();
                }

                _busy.MaybeSample();
                _work.MaybeSample();

                // if no jobs, check if a Set() arrived while we were draining; if not, go idle
                if (!didWork && !_wake.WaitOne(0))
                    break;
            }
        }

        _glfw.MakeContextCurrent(null);
        _glfw.DestroyWindow(_hiddenWin);
        _hiddenWin = null;
        _gl = null;
    }
}
