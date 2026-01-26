using Silk.NET.OpenGL;
using Silk.NET.GLFW;
using System.Collections.Concurrent;
using MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;
using MarcoZechner.CodeDrawDotNet.Api.Graphics.Enums;
using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Engine;

public unsafe abstract class AbstractWindowRenderer : IAttachableRenderer
{
    protected WindowHandle* Window;
    protected IWindowSettings? PublicWindow;
    protected string Title = "Title";
    protected IWindowHost? Host;

    protected Thread? Thread;
    private int _threadId;

    protected volatile bool Running;

    protected IRenderThreadCallbacks? Callbacks;

    // GL plumbing
    // protected Glfw Glfw => CodeDrawHost.Instance.Glfw;
    protected GL? Gl;

    // Metrics
    public long Frames { get; protected set; }
    public DateTime StartUtc { get; protected set; }
    public TimeSpan Uptime => (StartUtc == default) ? TimeSpan.Zero : DateTime.UtcNow - StartUtc;
    public int MaxInflightFrames { get; set; } = 3; // how many frames can be “in flight” (submitted but not yet presented)

    // ----- batching -----
    private readonly object _stagingLock = new();
    private readonly List<IRenderCommand> _staging = []; // current “recording”

    private readonly ConcurrentQueue<(long token, List<IRenderCommand> batch)> _frames = new();
    private long _nextToken = 0;

    private BlendMode2D _currentBlendModeForSync = BlendMode2D.RGB_BLEND_KEEP_DST_ALPHA;

    private readonly ConcurrentQueue<long> _inflightOrder = new();
    private int _inflightCount;
    /// <summary>Frames currently queued but not yet rendered.</summary>
    public int QueuedFrames => _frames.Count;

    /// <summary>Frames currently being rendered or presented.</summary>
    public int InflightFrames => Volatile.Read(ref _inflightCount);

    /// <summary>Total backlog = Queued + Inflight.</summary>
    public int BacklogFrames => QueuedFrames + InflightFrames;

    private readonly ConcurrentDictionary<long, ManualResetEventSlim> _frameWaiters = new();

    public double Fps => FpsGetter?.Invoke() ?? 0.0; // delegate injected by subclass

    protected Func<double>? FpsGetter;

    private int _fbW;
    private int _fbH;

    public void SetFramebufferSizeFromUi(int w, int h)
    {
        Volatile.Write(ref _fbW, w);
        Volatile.Write(ref _fbH, h);
    }

    protected (int w, int h) GetFramebufferSizeCached()
    {
        int w = Volatile.Read(ref _fbW);
        int h = Volatile.Read(ref _fbH);
        return (w, h);
    }

    private bool _resizeInProgress;

    public void SetResizeInProgressFromUi(bool v)
        => Volatile.Write(ref _resizeInProgress, v);

    protected bool IsResizeInProgress()
        => Volatile.Read(ref _resizeInProgress);

    protected AbstractWindowRenderer() {}

    internal void Attach(WindowHandle* window, string title)
    {
        if (Window != null)
            throw new InvalidOperationException("Renderer is already attached.");
        Window = window;
        Title = title;
    }

    public void Attach(IWindowHost host, nint native, string title, IRenderThreadCallbacks cb, IWindowSettings settings)
    {
        Host = host;
        Title = title;
        Window = (WindowHandle*)native;
        Callbacks = cb;
        PublicWindow = settings;
    }

    public void Enqueue(IRenderCommand cmd)
    {
        lock (_stagingLock) _staging.Add(cmd);
    }

    public void SetBlendModeForFrameSync(BlendMode2D mode) => _currentBlendModeForSync = mode;

    // seal staging into a frame; returns token
    public long SealFrame()
    {
        List<IRenderCommand> batch;
        lock (_stagingLock)
        {
            batch = _staging.Count > 0 ? [.. _staging] : [];
            _staging.Clear();
        }

        batch.Insert(0, new RenderStateSyncAction(_currentBlendModeForSync));

        var token = Interlocked.Increment(ref _nextToken);
        _frames.Enqueue((token, batch));
        _frameWaiters.GetOrAdd(token, _ => new ManualResetEventSlim(false));

        _inflightOrder.Enqueue(token);
        Interlocked.Increment(ref _inflightCount);
        return token;
    }

    public void WaitForPresented(long? frameToken = null)
    {
        if (IsRenderThread())
            throw new InvalidOperationException("WaitForRender cannot be called from the render thread.");

        if (frameToken is null)
        {
            var last = Volatile.Read(ref _nextToken);
            if (last == 0) return; // nothing submitted
            WaitForPresented(last);
            return;
        }

        if (_frameWaiters.TryGetValue(frameToken.Value, out var mres))
        {
            mres.Wait();
            _frameWaiters.TryRemove(frameToken.Value, out _);
        }
    }

    protected bool TryDequeueFrame(out long token, out List<IRenderCommand>? batch)
    {
        if (_frames.TryDequeue(out var item))
        {
            token = item.token;
            batch = item.batch;
            return true;
        }
        token = 0; batch = null;
        return false;
    }

    protected void SignalPresented(long token)
    {
        if (_frameWaiters.TryGetValue(token, out var mres))
        {
            mres.Set();
            _frameWaiters.TryRemove(token, out _);
        }

        // dequeue the head (presentation order == submission order)
        if (_inflightOrder.TryDequeue(out var head))
        {
            // optional sanity check:
            if (head != token) Console.WriteLine($"[WARN] Presented {token} but head was {head}");
        }
        Interlocked.Decrement(ref _inflightCount);
    }

    public void WaitForInflightSlot()
    {
        // Fast-path: no throttling
        if (MaxInflightFrames <= 0) return; // 0 or less = unlimited
        while (Volatile.Read(ref _inflightCount) >= MaxInflightFrames)
        {
            // wait on the oldest pending frame to finish
            if (_inflightOrder.TryPeek(out var oldest))
            {
                WaitForPresented(oldest);
            }
            else
            {
                Thread.Yield();
            }
        }
    }


    public void Start()
    {
        Running = true;
        Thread = new Thread(Main) { IsBackground = true, Name = $"Render-{Title}" };
        Thread.Start();
    }

    public void StopAndJoin()
    {
        Running = false;
        Thread?.Join();
        Thread = null;
    }

    private void Main()
    {
        _threadId = Environment.CurrentManagedThreadId;

        var host = CodeDrawHost.Instance;
        host.EnsureStarted();

        CodeDrawHost.Instance.WithGlfw(glfw => glfw.MakeContextCurrent(Window));
        CodeDrawHost.Instance.WithGlfw(glfw =>
        {
            glfw.GetFramebufferSize(Window, out var w, out var h);
            SetFramebufferSizeFromUi(w, h);
        });

        Gl = GL.GetApi(name => CodeDrawHost.Instance.WithGlfw(glfw => glfw.GetProcAddress(name)));

        Gl.Enable(EnableCap.DebugOutput);
        Gl.DebugMessageCallback((_, type, id, sev, len, msg, _) =>
        {
            if (sev == GLEnum.DebugSeverityNotification)
                return; // ignore
            var s = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(msg, len);
            Console.WriteLine($"[GL] {sev} {type} {id}: {s}");
        }, null);

        Gl2DResources.Install(Gl!, (nint)Window);

        StartUtc = DateTime.UtcNow;

        try
        {
            Callbacks?.OnLoaded(Gl!, null, (nint)Window); //TODO:  pass glfw again?
            RunLoop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RENDER THREAD CRASH] {Title}\n{ex}");
        }
        finally
        {
            Running = false;
            Gl2DResources.Uninstall(Gl!, (nint)Window);
            CodeDrawHost.Instance.WithGlfw(glfw => glfw.MakeContextCurrent(null));
        }
    }

    protected abstract void RunLoop();

    public bool IsRenderThread()
        => Environment.CurrentManagedThreadId == _threadId;
}
