using System.Diagnostics;
using MarcoZechner.Math;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace MarcoZechner.CodeDraw.Net;

public unsafe partial class GLFWWindow //: IDisposable
{
    #region Window Settings
    /// <summary>
    /// If true, the window will automatically swap buffers after each render call.
    /// </summary>
    public bool AutoRender { get; set; } = true;
    public int TargetFramerate { get; set; } = 0;
    public double TargetFrameTime => TargetFramerate > 0 ? 1000.0 / TargetFramerate : 0;
    #endregion



    private readonly TaskCompletionSource<bool> _setupTCS = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _renderTaskCTS = new();
    private readonly Task _renderTask;
    private WindowHandle* _windowHandle;
    public static int WindowCount { get; private set; } = 0;
    private static Glfw? _glfw;
    public static Glfw Glfw
    {
        get
        {
            if (_glfw == null)
            {
                throw new InvalidOperationException("GLFW is not initialized. Make sure to create at least one window before accessing GLFW.");
            }
            return _glfw;
        }
    }

    private GL _gl;
    public GL GL => _gl;
    private GRContext? _grContext;
    private GRGlFramebufferInfo _fbInfo;
    private GRBackendRenderTarget? _backendRenderTarget;
    private SKSurface? _surface;

    public event Action<Vector2<int>>? Resize;
    // public event Action? Closing;
    // public event Action<bool>? FocusChanged;
    /// <summary>
    /// Called once after the OpenGL context has been created and before the first frame is rendered.
    /// After this event, the constructor will return control to the caller.
    /// </summary>
    private event Action? Load;
    //TODO implement
    // /// <summary>
    // /// 
    // /// </summary>
    // public event Action<double>? Update;
    /// <summary>
    /// Called before every frame gets rendered in the window
    /// <para>Related Settings:</para>
    /// <list type="bullet">
    /// <see cref="AutoRender"/>
    /// </list>
    /// </summary>
    public event Action<double>? Render;

    public event Action? Closing;

    public GLFWWindow(Action? onLoad = null)
    {
        if (onLoad != null)
            Load += onLoad;

        if (WindowCount == 0)
            InitializeGLFW();

        _renderTask = Task.Factory.StartNew(
            SetupRenderLoop,
            _renderTaskCTS.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );

        _setupTCS.Task.Wait();
        if (_glfw == null || _gl == null || _windowHandle == null)
        {
            throw new Exception("Failed to initialize GLFW or OpenGL.");
        }

        WindowCount++;
    }

    //TODO: Fatal Error xD
    // public void Dispose()
    // {
    //     _renderTaskCTS.Cancel();
    //     _renderTask.Wait();
    //     _renderTaskCTS.Dispose();
    //     _setupTCS.TrySetCanceled();
    //     _renderTask.Dispose();
    //     _surface?.Dispose();
    //     _grContext?.Dispose();
    //     _backendRenderTarget?.Dispose();
    //     _glfw?.DestroyWindow(_windowHandle);

    //     WindowCount--;
    //     if (WindowCount == 0)
    //     {
    //         _glfw?.Terminate();
    //         _glfw = null;
    //     }

    //     GC.SuppressFinalize(this); //TODO what is this, was a "hint" by the IDE?
    // }

    private static void InitializeGLFW()
    {
        _glfw = Glfw.GetApi();
        _glfw.Init();
        _glfw.WindowHint(WindowHintBool.TransparentFramebuffer, true);
        _glfw.WindowHint(WindowHintBool.Resizable, true);
        _glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGL);
        _glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        _glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        _glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
    }

    private static WindowHandle* CreateWindow()
    {
        if (_glfw == null)
            throw new InvalidOperationException("GLFW is not initialized.");
        var windowHandle = _glfw.CreateWindow(800, 600, "title", null, null);
        return windowHandle switch
        {
            null => throw new InvalidOperationException("Failed to create GLFW window."),
            _ => windowHandle,
        };
    }

    private void SetupRenderLoop()
    {
        try
        {
            // needs to be called in the same thread as the render loop...
            _windowHandle = CreateWindow();

            Glfw.MakeContextCurrent(_windowHandle);

            Glfw.SetErrorCallback((error, description) =>
            {
                Console.WriteLine($"GLFW Error: {error} - {description}");
            });

            Glfw.SetWindowCloseCallback(_windowHandle, (w) =>
            {
                Closing?.Invoke();
                WindowCount--;
            });

            #region Attempt to rendering window while its being moved or resized
            Glfw.SetWindowPosCallback(_windowHandle, (w, x, y) =>
            {
                RenderCall(false);
            });

            Glfw.SetWindowSizeCallback(_windowHandle, (w, width, height) =>
            {
                ResizeSkiaSurface();
                Resize?.Invoke(new Vector2<int>(width, height));
                RenderCall(false);
            });

            Glfw.SetWindowRefreshCallback(_windowHandle, (w) =>
            {
                ResizeSkiaSurface();
                RenderCall(false);
            });
            #endregion

            InitGL();
            Load?.Invoke();

            _setupTCS.TrySetResult(true);


            // 4) Main Loop
            RenderLoop();

            // 5) Cleanup
            Glfw.DestroyWindow(_windowHandle);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RenderLoop: {ex}");
        }
    }

    private void InitGL()
    {
        _gl = GL.GetApi(Glfw.GetProcAddress);

        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);
        uint framebuffer = 0;
        _gl.GetInteger(GLEnum.FramebufferBinding, framebuffer);
        _fbInfo = new GRGlFramebufferInfo(framebuffer, SKColorType.Rgba8888.ToGlSizedFormat());

        _gl.Enable(GLEnum.Blend);
        _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        ResizeSkiaSurface();
    }

    private void ResizeSkiaSurface()
    {
        _surface?.Dispose();
        Glfw.GetFramebufferSize(_windowHandle, out var w, out var h);
        _backendRenderTarget = new GRBackendRenderTarget(w, h, 0, 8, _fbInfo);
        _surface = SKSurface.Create(_grContext, _backendRenderTarget,
            GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }

    private double _dtInternalRender = 0;
    private double _dtClient = 0;
    private double _dtLoop = 0;
    private double _dt;
    private readonly Stopwatch _stopwatch = new();


    private void RenderLoop()
    {
        //Debugging
        Task.Run(() =>
        {
            while (!_renderTaskCTS.IsCancellationRequested && !Glfw.WindowShouldClose(_windowHandle))
                RenderMonitor();
        });
        //==========

        while (!_renderTaskCTS.IsCancellationRequested && !Glfw.WindowShouldClose(_windowHandle))
        {
            RenderCall(true);
        }
    }

    private void RenderCall(bool processesEvents)
    {
        _stopwatch.Stop();
        _dtLoop = _stopwatch.Elapsed.TotalMilliseconds;
        _stopwatch.Restart();
        _dt = _dtInternalRender + _dtClient + _dtLoop;
        if (TargetFrameTime > 0 && _dt < TargetFrameTime)
        {
            int sleepTime = (int)(TargetFrameTime - _dt);
            if (sleepTime > 0)
                Thread.Sleep(sleepTime);
            _dt = TargetFrameTime;
        }

        _stopwatch.Restart();

        Render?.Invoke(_dt);
        _stopwatch.Stop();
        _dtClient = _stopwatch.Elapsed.TotalMilliseconds;


        _stopwatch.Restart();
        if (_surface == null || _grContext == null)
            throw new InvalidOperationException("Surface or GRContext is not initialized.");

        if (processesEvents)
            Glfw.PollEvents();

        var canvas = _surface.Canvas;

        RenderPendingActions();

        if (AutoRender)
        {
            canvas.Flush();
            _grContext.Flush();
            Glfw.SwapBuffers(_windowHandle);
        }
        _stopwatch.Stop();
        _dtInternalRender = _stopwatch.Elapsed.TotalMilliseconds;
        _stopwatch.Restart();
    }

    private void RenderPendingActions()
    {
    }




    #region Debugging
    private readonly List<double> _fpsTimes = [];
    private readonly Stopwatch _consolePrintStopwatch = Stopwatch.StartNew();
    private void RenderMonitor()
    {
        // Debug rendering time
        int max = 150;
        int fps = (int)(1000.0 / _dt);
        _fpsTimes.Add(_dt);
        if (_fpsTimes.Count > 100)
            _fpsTimes.RemoveAt(0);
        int fpsAvg = (int)(1000.0 / _fpsTimes.Average());

        if (_consolePrintStopwatch.ElapsedMilliseconds < 1000 / 10) // 10 times per second
            return;

        Console.Write('[');
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(new string('#', (int)_dtInternalRender));
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(new string('#', (int)_dtClient));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(new string('#', (int)_dtLoop));
        Console.ResetColor();
        Console.Write(new string(' ', (int)MathF.Max(0, max - (int)_dtInternalRender - (int)_dtClient - (int)_dtLoop)));
        Console.ResetColor();
        Console.Write($"] {_dt:00.00}ms (int: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{_dtInternalRender:00.00}");
        Console.ResetColor();
        Console.Write("ms, client: ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"{_dtClient:00.00}");
        Console.ResetColor();
        Console.Write("ms, loop: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{_dtLoop:00.00}");
        Console.ResetColor();
        Console.Write("ms) - ");
        Console.ForegroundColor = fps switch
        {
            >= 60 => ConsoleColor.Green,
            >= 30 => ConsoleColor.Yellow,
            _ => ConsoleColor.Red,
        };
        Console.Write($"{fps}");
        Console.ResetColor();
        Console.Write(" FPS - Avg: ");
        Console.ForegroundColor = fpsAvg switch
        {
            >= 60 => ConsoleColor.Green,
            >= 30 => ConsoleColor.Yellow,
            _ => ConsoleColor.Red,
        };
        Console.Write($"{fpsAvg}");
        Console.ResetColor();
        Console.WriteLine(" FPS");

        _consolePrintStopwatch.Restart();
    }
    #endregion
}