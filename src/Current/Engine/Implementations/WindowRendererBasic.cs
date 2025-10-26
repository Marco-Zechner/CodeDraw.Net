using Silk.NET.OpenGL;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Engine;

internal unsafe sealed class WindowRendererBasic
{
    private readonly WindowHandle* _window;
    private readonly string _title;
    private Thread? _thread;
    private volatile bool _running;

    private CodeDrawWindow? _publicWindow;

    public long Frames { get; private set; }
    public DateTime StartUtc { get; private set; }
    public TimeSpan Uptime => (StartUtc == default) ? TimeSpan.Zero : DateTime.UtcNow - StartUtc;


    public WindowRendererBasic(WindowHandle* window, string title)
    {
        _window = window;
        _title = title;
    }

    public void BindPublic(CodeDrawWindow w) => _publicWindow = w;

    public void Start()
    {
        _running = true;
        _thread = new Thread(Main) { IsBackground = true, Name = $"Render-{_title}" };
        _thread.Start();
    }

    private void Main()
    {
        var host = CodeDrawHost.Instance;
        host.EnsureStarted();

        var glfw = host.Glfw;
        glfw.MakeContextCurrent(_window);
        var gl = GL.GetApi(glfw.GetProcAddress);
        var gfx = new GraphicsImpl(gl);

        StartUtc = DateTime.UtcNow;

        // Fire events
        CodeDrawEvents.RaiseOnWindowLoaded(_publicWindow!, gfx);
        _publicWindow!.RaiseLoaded(gfx);

        double last = 0;

        while (_running && !glfw.WindowShouldClose(_window))
        {
            double now = (DateTime.UtcNow - StartUtc).TotalSeconds;
            double dt = now - last; last = now;

            CodeDrawEvents.RaiseBeforeAnyWindowRender(_publicWindow!, gfx, dt);
            _publicWindow!.RaiseBeforeRender(gfx, dt);

            // For Step 1, do nothing if user didn’t clear/draw; just swap.
            glfw.SwapBuffers(_window);
            Frames++;

            // basic pacing
            var target = _publicWindow!.TargetFPS;
            if (target > 0)
            {
                int ms = (int)MathF.Max(0, (int)MathF.Round((float)(1000.0 / target)));
                if (ms > 0) Thread.Sleep(ms);
            }
        }

        glfw.MakeContextCurrent(null);
    }
}
