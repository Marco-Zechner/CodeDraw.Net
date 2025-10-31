using Silk.NET.OpenGL;
using Silk.NET.GLFW;
using System.Collections.Concurrent;
using MarcoZechner.CodeDrawDotNet.Api.Graphics;
using MarcoZechner.CodeDrawDotNet.Api;

namespace MarcoZechner.CodeDrawDotNet.Engine.Implementations;

public unsafe abstract class AbstractWindowRenderer
{
    protected WindowHandle* Window;
    protected string Title = "Title";

    protected Thread? Thread;
    protected int ThreadId;
    protected volatile bool Running;

    protected CodeDrawWindowBase? PublicWindow;

    // GL plumbing
    protected Glfw Glfw => CodeDrawHost.Instance.Glfw;
    protected GL? GL;

    // Metrics
    public long Frames { get; protected set; }
    public DateTime StartUtc { get; protected set; }
    public TimeSpan Uptime => (StartUtc == default) ? TimeSpan.Zero : DateTime.UtcNow - StartUtc;

    // ----- batching -----
    private readonly object _stagingLock = new();
    private readonly List<IRenderAction> _staging = []; // current “recording”

    private readonly ConcurrentQueue<(long token, List<IRenderAction> batch)> _frames = new();
    private long _nextToken = 0;

    private readonly ConcurrentDictionary<long, ManualResetEventSlim> _frameWaiters = new();

    public double Fps => _fpsGetter?.Invoke() ?? 0.0; // delegate injected by subclass

    protected Func<double>? _fpsGetter;

    protected AbstractWindowRenderer() {}

    protected AbstractWindowRenderer(WindowHandle* window, string title)
    {
        Window = window;
        Title = title;
    }

    internal void Attach(WindowHandle* window, string title)
    {
        if (Window != null)
            throw new InvalidOperationException("Renderer is already attached.");
        Window = window;
        Title = title;
    }

    public void BindPublic(CodeDrawWindowBase w) => PublicWindow = w;

    public void Enqueue(IRenderAction action)
    {
        lock (_stagingLock) _staging.Add(action);
    }

    // seal staging into a frame; returns token
    public long SealFrame()
    {
        List<IRenderAction> batch;
        lock (_stagingLock)
        {
            batch = _staging.Count > 0 ? [.. _staging] : [];
            _staging.Clear();
        }

        var token = Interlocked.Increment(ref _nextToken);
        _frames.Enqueue((token, batch));
        _frameWaiters.GetOrAdd(token, _ => new ManualResetEventSlim(false));
        return token;
    }

    public void WaitForPresented(long? frameToken = null)
    {
        if (Environment.CurrentManagedThreadId == ThreadId)
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

    // AbstractWindowRenderer.cs (private helpers)
    protected bool TryDequeueFrame(out long token, out List<IRenderAction>? batch)
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
        ThreadId = Environment.CurrentManagedThreadId;

        var host = CodeDrawHost.Instance;
        host.EnsureStarted();

        Glfw.MakeContextCurrent(Window);
        GL = GL.GetApi(Glfw.GetProcAddress);

        StartUtc = DateTime.UtcNow;

        // Announce ready
        PublicWindow!.RaiseLoaded(GL!, Glfw, Window);
        CodeDrawEvents.RaiseLoaded(PublicWindow!, GL!, Glfw, Window);

        PublicWindow.SignalLoadedComplete();

        RunLoop(); // delegate to subclass

        Running = false;

        Glfw.MakeContextCurrent(null);
    }

    public static bool IsRenderThread(AbstractWindowRenderer r)
    => Environment.CurrentManagedThreadId == r.ThreadId;

    protected abstract void RunLoop();
}
