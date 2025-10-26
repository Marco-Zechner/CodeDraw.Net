using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet;

public sealed class SharedGlManager : IDisposable
{
    #region GLConfig

    public sealed class GlConfig
    {
        public int Major = 3;
        public int Minor = 3;
        public OpenGlProfile Profile = OpenGlProfile.Core;
        public bool ForwardCompat = false;        // true only for macOS

        public int RedBits = 8, GreenBits = 8, BlueBits = 8, AlphaBits = 8;
        public int DepthBits = 24, StencilBits = 8;
        public int Samples = 0;

        public bool TransparentFramebuffer = true;  // <-- start with false for debugging
        public bool Resizable = true;
        public bool Decorated = true;
        public bool Doublebuffer = true;
        public bool DebugContext = true;            // <-- start false
    }
    public GlConfig Config { get; } = new();

    public void ApplyWindowHints()
    {
        var glfw = Glfw;

        // API/version/profile
        glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGL);
        glfw.WindowHint(WindowHintInt.ContextVersionMajor, Config.Major);
        glfw.WindowHint(WindowHintInt.ContextVersionMinor, Config.Minor);
        glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, Config.Profile);
        glfw.WindowHint(WindowHintBool.OpenGLForwardCompat, Config.ForwardCompat);
        glfw.WindowHint(WindowHintBool.OpenGLDebugContext, Config.DebugContext);

        // Pixel format (must be compatible across share group)
        glfw.WindowHint(WindowHintInt.RedBits,     Config.RedBits);
        glfw.WindowHint(WindowHintInt.GreenBits,   Config.GreenBits);
        glfw.WindowHint(WindowHintInt.BlueBits,    Config.BlueBits);
        glfw.WindowHint(WindowHintInt.AlphaBits,   Config.AlphaBits);
        glfw.WindowHint(WindowHintInt.DepthBits,   Config.DepthBits);
        glfw.WindowHint(WindowHintInt.StencilBits, Config.StencilBits);
        glfw.WindowHint(WindowHintInt.Samples,     Config.Samples);

        // Behavior/visual
        glfw.WindowHint(WindowHintBool.TransparentFramebuffer, Config.TransparentFramebuffer);
        glfw.WindowHint(WindowHintBool.Resizable, Config.Resizable);
        glfw.WindowHint(WindowHintBool.Decorated, Config.Decorated);
        glfw.WindowHint(WindowHintBool.DoubleBuffer, Config.Doublebuffer);
    }

    #endregion



    // singleton-ish access optional
    public static SharedGlManager Instance { get; } = new();

    private readonly object _stateLock = new();
    private Thread? _thread;
    private volatile bool _running;
    private readonly AutoResetEvent _jobAvailable = new(false);
    private readonly ConcurrentQueue<Func<GL, Task>> _jobs = new();

    private readonly ManualResetEventSlim _ready = new(false); // signals GLFW-inited + share-root created
    private int _windowRefs; // refcount of windows using the manager

    public GL? GLOnManager { get; private set; } // valid only on manager thread

    public unsafe WindowHandle* ShareWindow { get; private set; } = null;
    public Glfw Glfw { get; private set; } = null!;

    private readonly object _shareGroupLock = new();
    public object ShareGroupLock => _shareGroupLock;

    private SharedGlManager()
    {
        EnsureStarted();
    }

    // ---------- public API ----------

    /// <summary>
    /// Acquire the share window for creating a new visible window
    /// </summary>
    /// <returns></returns>
    public unsafe WindowHandle* Acquire()
    {
        EnsureStarted();
        Interlocked.Increment(ref _windowRefs);
        // wait until share root exists
        _ready.Wait();
        return ShareWindow;
    }

    /// <summary>
    /// Release when a visible window is destroyed
    /// </summary>
    public void Release()
    {
        if (Interlocked.Decrement(ref _windowRefs) == 0)
        {
            // schedule shutdown on manager thread
            Enqueue(async gl => await ShutdownCoreAsync());
        }
    }

    public void WaitForOpenWindows()
    {
        while (_running)
        {
            Thread.Sleep(20);
        }
    }

    /// Enqueue work that mutates shared GL resources (runs on manager thread)
    public Task Enqueue(Func<GL, Task> job)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _jobs.Enqueue(async gl => { await job(gl); tcs.SetResult(null); });
        _jobAvailable.Set();
        return tcs.Task;
    }

    public Task<T> Enqueue<T>(Func<GL, Task<T>> job)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _jobs.Enqueue(async gl => { var r = await job(gl); tcs.SetResult(r); });
        _jobAvailable.Set();
        return tcs.Task;
    }

    public void Dispose() => ForceStop();

    // ---------- internals ----------

    private void EnsureStarted()
    {
        lock (_stateLock)
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(ThreadMain) { IsBackground = true, Name = "SharedGLFW+GL" };
            _thread.Start();
        }
        // caller can immediately call Acquire(); _ready will block until root is made
    }

    private void ForceStop()
    {
        lock (_stateLock)
        {
            if (!_running) return;
            _running = false;
            _jobAvailable.Set();
        }
        _thread?.Join();
        _thread = null;
    }

    private unsafe async Task ShutdownCoreAsync()
    {
        // destroy share window + terminate GLFW on manager thread
        if (ShareWindow != null)
        {
            Glfw.MakeContextCurrent(null);
            Glfw.DestroyWindow(ShareWindow);
            ShareWindow = null;
        }

        // let the queue drain, then stop loop and terminate
        _running = false;
        _jobAvailable.Set();
    }

    private unsafe void ThreadMain()
    {
        Glfw = Glfw.GetApi();

        // ---------- GLFW init + share-root creation on this thread ----------
        if (!Glfw.Init()) throw new InvalidOperationException("GLFW init failed.");

        Glfw.SetErrorCallback((error, description) =>
        {
            Console.WriteLine($"GLFW Manager Error: {error} - {description}");
        });

        lock (_shareGroupLock)
        {
            ApplyWindowHints();

            ShareWindow = Glfw.CreateWindow(1, 1, "share-root", null, null);
            Glfw.HideWindow(ShareWindow);
            Glfw.MakeContextCurrent(ShareWindow);

            GLOnManager = GL.GetApi(Glfw.GetProcAddress);

            GLOnManager.Enable(GLEnum.DebugOutput);
            GLOnManager.Enable(GLEnum.DebugOutputSynchronous);
            unsafe {
            GLOnManager.DebugMessageCallback((source, type, id, severity, length, message, userparam) => {
                string msg = Marshal.PtrToStringAnsi(message, length);
                Logger.LogLine($"[DebugMessageCallback] source: {source}, type: {type}, id: {id}, severity {severity}, length {length}, userParam {userparam}\n{msg}");
            }, (void*) 0);
            }
            // (optional) global GL setup on shared context:
            // GLOnManager.Enable(GLEnum.DebugOutput); ...

            try
            {
                var ver = GLOnManager.GetStringS(GLEnum.Version);
                var ven = GLOnManager.GetStringS(GLEnum.Vendor);
                var ren = GLOnManager.GetStringS(GLEnum.Renderer);
                Logger.LogLine($"[SharedGL] Root context: {ver} | {ven} | {ren}");
            }
            catch { /* ignore */ }

            Glfw.MakeContextCurrent(null);
        }

        _ready.Set(); // other threads may now CreateWindow(..., share=ShareWindow)

        // ---------- job loop ----------
        while (_running)
        {
            if (_jobs.TryDequeue(out var job))
            {
                lock (_shareGroupLock)
                {
                    Glfw.MakeContextCurrent(ShareWindow);
                    try
                    {
                        job(GLOnManager!).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SharedGlManager: job threw exception: {ex}");
                    }
                    finally
                    {
                        Glfw.MakeContextCurrent(null);
                    }
                }
            }
            else
            {
                _jobAvailable.WaitOne(8);
            }
        }

        // drain any late jobs
        while (_jobs.TryDequeue(out var job))
        {
            try
            {
                job(GLOnManager!).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SharedGlManager: late job threw exception: {ex}");
            }
        }

        // final cleanup (if not already done)
        if (ShareWindow != null)
        {
            Glfw.MakeContextCurrent(null);
            Glfw.DestroyWindow(ShareWindow);
            ShareWindow = null;
        }

        Glfw.Terminate();
        GLOnManager = null;
        _ready.Reset();
    }
}