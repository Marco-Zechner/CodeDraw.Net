using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet.Old1;

public unsafe partial class GLFWWindow
{
    private bool _renderNextFrame = false;
    private bool _nextFrameRendered = false;
    private Color _clearColor = Color.WHITE;
    private readonly TaskCompletionSource<bool> _initTCS = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _onLoadTCS = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _runTCS = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _renderTaskCTS = new();
    private readonly Task? _renderTask;
    private WindowHandle* _windowHandle;
    public WindowHandle* WindowHandle => _windowHandle;
    private readonly SharedGlManager _mgr = SharedGlManager.Instance;
    private readonly unsafe WindowHandle* _shareHandle;

    private GL? _gl;
    public GL GL => _gl ?? throw new InvalidOperationException("OpenGL is not initialized. Make sure to create at least one window before accessing OpenGL.");
    private GRContext? _grContext;
    private GRGlFramebufferInfo _fbInfo;
    private GRBackendRenderTarget? _backendRenderTarget;
    private SKSurface? _surface;
    public bool IsRunning => !_mgr.Glfw.WindowShouldClose(_windowHandle) && !_renderTaskCTS.IsCancellationRequested;

    public event Action<Vector2<int>>? OnResize;
    private bool _resizeEndPending = false;
    public event Action<Vector2<int>>? OnResizeEnd;
    public event Action<Vector2<int>>? OnMove;
    /// <summary>
    /// true means it gained focus. <para>
    /// false means it lost focus. </para>
    /// </summary>
    public event Action<bool>? OnFocusChanged;

    public event Action? OnClosing;

    public Input Input { get; private set; } = null!;


    #region ManagementEvents
    /// <summary>
    /// <para>if true, the window will be created but not rendering until Run() is called.</para>
    /// It will then use its internal render loop and provide events "OnLoad" and "OnRender"<br></br>
    /// OnLoad will be called once after the OpenGL context has been created and before the first frame is rendered.<br></br>
    /// OnRender will be called before every frame gets rendered in the window, there you can do your drawing.
    /// </summary>
    public readonly bool UseManagementEvents = false;

    private event Action? _onLoad;
    // public event Action? Closing;
    // public event Action<bool>? FocusChanged;
    /// <summary>
    /// Called once after the OpenGL context has been created and before the first frame is rendered.
    /// After this event, the constructor will return control to the caller.
    /// </summary>
    public event Action? OnLoad
    {
        add
        {
            if (UseManagementEvents)
                _onLoad += value;
            else
                throw new InvalidOperationException("OnLoad can only be used when UseManagementEvents is set to true in the constructor.");
        }
        remove
        {
            if (UseManagementEvents)
                _onLoad -= value;
            else
                throw new InvalidOperationException("OnLoad can only be used when UseManagementEvents is set to true in the constructor.");
        }
    }

    private event Action<double, SKCanvas, GL>? _onRender;
    /// <summary>
    /// Called before every frame gets rendered in the window
    /// <para>Related Settings:</para>
    /// <list type="bullet">
    /// <see cref="AutoRender"/>
    /// <see cref="AutoClear"/> 
    /// </list>
    /// </summary>
    public event Action<double, SKCanvas, GL>? OnRender
    {
        add
        {
            if (UseManagementEvents)
                _onRender += value;
            else
                throw new InvalidOperationException("OnRender can only be used when UseManagementEvents is set to true in the constructor.");
        }
        remove
        {
            if (UseManagementEvents)
                _onRender -= value;
            else
                throw new InvalidOperationException("OnRender can only be used when UseManagementEvents is set to true in the constructor.");
        }
    }

    #endregion
    
    internal GLFWWindow(string title = "title", bool useManagementEvents = false)
    {
        _title = title;
        UseManagementEvents = useManagementEvents;

        _shareHandle = _mgr.Acquire();

        _renderTask = Task.Factory.StartNew(
            SetupRenderLoop,
            _renderTaskCTS.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );

        _initTCS.Task.Wait();
        if (_mgr.Glfw == null || _gl == null || _windowHandle == null)
            throw new Exception("Failed to initialize GLFW or OpenGL.");
    }

    public void Run()
    {
        if (!UseManagementEvents)
            throw new InvalidOperationException("Run can only be called when UseManagementEvents is set to true in the constructor.");

        _runTCS.TrySetResult(true); // signal that Run has been called, allow render loop to continue
        _onLoadTCS.Task.Wait(); // wait until setup is done
        if (_mgr.Glfw == null || _gl == null || _windowHandle == null)
        {
            throw new Exception("Failed to initialize GLFW or OpenGL.");
        }
    }



    private WindowHandle* CreateWindow()
    {
        if (_mgr.Glfw == null)
            throw new InvalidOperationException("GLFW is not initialized.");

        if (_shareHandle == null) throw new InvalidOperationException("Share root not ready.");

        _mgr.ApplyWindowHints();

        var windowHandle = _mgr.Glfw.CreateWindow(800, 600, _title, null, _shareHandle);

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
            _windowHandle = CreateWindow(); // needs to be called in the same thread as the render loop...

            _mgr.Glfw.MakeContextCurrent(_windowHandle);

            Input = new Input(this);

            #region Callbacks

            _mgr.Glfw.SetWindowCloseCallback(_windowHandle, (w) =>
            {
                OnClosing?.Invoke();
            });

            _mgr.Glfw.SetFramebufferSizeCallback(_windowHandle, (w, x, y) =>
            {
                ResizeSkiaSurface();
                RenderCall(false);
            });


            _mgr.Glfw.SetWindowFocusCallback(_windowHandle, (w, focus) =>
            {
                OnFocusChanged?.Invoke(focus);
                if (!focus)
                {
                    Input.ClearHoldKeys();
                }
            });

            #endregion


            #region Attempt to rendering window while its being moved or resized
            _mgr.Glfw.SetWindowPosCallback(_windowHandle, (w, x, y) =>
            {
                OnMove?.Invoke(new Vector2<int>(x, y));
                RenderCall(false);
            });

            _mgr.Glfw.SetWindowSizeCallback(_windowHandle, (w, width, height) =>
            {
                _resizeEndPending = true;
                OnResize?.Invoke(new Vector2<int>(width, height));
                RenderCall(false);
            });

            _mgr.Glfw.SetWindowRefreshCallback(_windowHandle, (w) =>
            {
                RenderCall(false);
            });
            #endregion

            InitGL();
            _initTCS.TrySetResult(true); // signal that GLFW and OpenGL are initialized, allow main thread to continue

            if (UseManagementEvents)
            {
                _runTCS.Task.Wait();
                _onLoad?.Invoke();
                _onLoadTCS.TrySetResult(true); // signal that setup is done, allow main thread to continue        
            }

            // 4) Main Loop
            RenderLoop();

            // 5) Cleanup
            _mgr.Glfw.DestroyWindow(_windowHandle);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RenderLoop: {ex}");
        }
    }

    private void InitGL()
    {
        _gl = GL.GetApi(_mgr.Glfw.GetProcAddress);

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
        _mgr.Glfw.GetFramebufferSize(_windowHandle, out var w, out var h);
        _backendRenderTarget = new GRBackendRenderTarget(w, h, 0, 8, _fbInfo);
        _surface = SKSurface.Create(_grContext, _backendRenderTarget,
            GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }


    private void RenderLoop()
    {
        Task.Run(() =>
        {
            while (!_renderTaskCTS.IsCancellationRequested && !_mgr.Glfw.WindowShouldClose(_windowHandle))
            {
                if (MonitorRendering)
                    RenderMonitor();
                else
                    Thread.Sleep(500);
            }
        });

        while (!_renderTaskCTS.IsCancellationRequested && !_mgr.Glfw.WindowShouldClose(_windowHandle))
        {
            RenderCall(true);
        }
    }

    private double _debtMs = 0;             // accumulated overtime (can be clamped)
    private const double MAX_DEBT_FRAMES = 10000; // safety cap: at most 5 frames of debt

    private void RenderCall(bool processesEvents)
    {
        if (processesEvents && _resizeEndPending)
        {
            _resizeEndPending = false;
            OnResizeEnd?.Invoke(Size);
        }

        _stopwatch.Stop();
        _dtLoop = _stopwatch.Elapsed.TotalMilliseconds;
        _stopwatch.Restart();
        _dt = _dtInternalRender + _dtLoop;
        _dtWait = 0;
        if (TargetFrameTime > 0)
        {
            if (_dt < TargetFrameTime)
            {
                double dtSpare = TargetFrameTime - _dt;
                double dtRepay = MathG.Min(dtSpare, _debtMs);
                _debtMs -= dtRepay;

                _dtWait = dtSpare - dtRepay;

                if (_dtWait > 0)
                {
                    Thread.Sleep((int)_dtWait);
                }
                _dt += _dtWait + dtRepay;
            }
            else
            {
                _debtMs += _dt - TargetFrameTime;

                double maxDebt = TargetFrameTime * MAX_DEBT_FRAMES;
                if (_debtMs > maxDebt)
                    _debtMs = maxDebt;
            }
        }
        
        int fps = (int)(1000.0 / _dt);
        _fpsTimes.Add(_dt);
        if (_fpsTimes.Count > 1000)
            _fpsTimes.RemoveAt(0);
        int fpsAvg = (int)(1000.0 / _fpsTimes.Average());

        Title = $"{_dt:00.00}/{_debtMs:00.00}ms ({fps} FPS, Avg: {fpsAvg} FPS) - {FrameCount}";

        _stopwatch.Restart();
        if (_surface == null || _grContext == null)
            throw new InvalidOperationException("Surface or GRContext is not initialized.");

        if (processesEvents)
        {
            Input.ResetFrameInputState();
            _mgr.Glfw.PollEvents();
        }

        var canvas = _surface.Canvas;

        if (AutoClear)
        {
            if (_clearColor.A >= 1)
                canvas.Clear(_clearColor.ToSkia());
            else
                canvas.Clear(new SKColor(0, 0, 0, 0));
        }

        if (UseManagementEvents)
            _onRender?.Invoke(_dt, canvas, GL);

        Render(_dt, canvas, GL);

        if (AutoRender || _renderNextFrame || !processesEvents)
        {
            _renderNextFrame = false;
            canvas.Flush();
            _grContext.Flush();
            _mgr.Glfw.SwapBuffers(_windowHandle);
            FrameCount++;
            _nextFrameRendered = true;
        }
        else
        {
            _mgr.Glfw.WaitEvents();
        }

        _stopwatch.Stop();
        _dtInternalRender = _stopwatch.Elapsed.TotalMilliseconds;
        _stopwatch.Restart();
    }

    protected virtual void Render(double dt, SKCanvas canvas, GL gl)
    {
        
    }

    /// <summary>
    /// Waits this the next frame is rendered, then returns.
    /// </summary>
    public virtual void Show()
    {
        _renderNextFrame = true;
        _mgr.Glfw.PostEmptyEvent();
        _nextFrameRendered = false;
        while (!_nextFrameRendered)
        {
            Task.Delay(5).Wait();
        }
        // Logger.LogLine($"{FrameCount, -8} Show() return");
    }

    public virtual void Clear(Color? clearColor = null)
    {
        if (clearColor == null) _clearColor = Color.BLACK;
        else _clearColor = clearColor;
    }

    public void Close()
    {
        if (_windowHandle != null && !_mgr.Glfw.WindowShouldClose(_windowHandle))
            _mgr.Glfw.SetWindowShouldClose(_windowHandle, true);

        _renderTaskCTS.Cancel();
        try { _renderTask?.Wait(); }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
        {
            Console.WriteLine("Render task cancelled with exception: " + ex.InnerException);
        }
        _renderTaskCTS.Dispose();

        _surface?.Dispose();
        _backendRenderTarget?.Dispose();
        _grContext?.Dispose();

        _mgr.Glfw.MakeContextCurrent(null);

        // IMPORTANT: tell the manager this window is gone (may trigger global shutdown)
        _mgr.Release();
    }
}