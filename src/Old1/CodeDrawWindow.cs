using System.Runtime.InteropServices;
using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet.Old1;

public partial class CodeDrawWindow
{
    private WindowOptions _windowOptions;
    private readonly IWindow _window;
    public Vector2 Size {
        get => _window.Size.ToVector2();
        set => _window.Size = value.ToSilkI();
    }
    public Vector2 Position {
        get => _window.Position.ToVector2();
        set => _window.Position = value.ToSilkI();
    }
    public string Title {
        get => _window.Title;
        set => _window.Title = value;
    }
    private GL? _gl;
    private GRContext? _grContext;
    private GRGlFramebufferInfo _fbInfo;
    private GRBackendRenderTarget? _backendRenderTarget;
    private SKSurface? _surface;

    public bool IsInstantDraw { get; set; } = false;
    private bool _drawnNextFrame = false;
    public int LineWidth { get; set; } = 1;
    public Color DrawColor { get; set; } = Color.BLACK;
    public CornerStyle CornerStyle { get; set; }
    public int CornerRadius { get; set; } = 0;
    public bool IsAntiAliased { get; set; }
    public TextFormat TextFormat { get; set; } = new TextFormat();

    private readonly DrawQueue _drawQueue = new();
    private readonly DrawQueue _drawBuffer = new();
    private Color _clearColor = Color.WHITE;
    /// <summary>
    /// Matrix that can be directly accessed and set by the User
    /// </summary>
    public Matrix3x3 WindowMatrix {get; set;} = Matrix3x3.Identity;
    private Vector2 _canvaseScaleOriginalSize = new(600, 600);
    private bool _scaleCanvasWithWindow = false;
    public bool ScaleCanvasWithWindow {
        get => _scaleCanvasWithWindow;
        set {
            _scaleCanvasWithWindow = value;
            if (value) {
                _canvaseScaleOriginalSize = _window.Size.ToVector2();
            }
        }
    }
    public Vector2 Origin { get; set; } = Vector2.Zero;
    public bool FlipY { get; set; } = false;
    public bool FlipX { get; set; } = false;

    public CodeDrawWindow(float xLeft = -1, float yTop = -1, float width = 600, float height = 600, string title = "CodeDraw") : this(
        new Vector2(xLeft, yTop), 
        new Vector2(width, height), 
        title
    ) {}

    public CodeDrawWindow() : this(
        new Vector2(-1, -1), 
        new Vector2(600, 600), 
        "CodeDraw"
    ) {}

    public CodeDrawWindow(string title = "CodeDraw") : this(
        new Vector2(-1, -1), 
        new Vector2(600, 600), 
        title
    ) {}

    public CodeDrawWindow(Vector2? position = null, Vector2? size = null, string title = "CodeDraw") : this(
        new CodeDrawOptions()
        {
            Position = position ?? new Vector2(-1, -1),
            Size = size ?? new Vector2(600, 600),
            Title = title
        }
    ) {}

    public CodeDrawWindow(CodeDrawOptions options)
    {
        _windowOptions = options;
        _window = Window.Create(_windowOptions);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Closing += OnClose;
        _window.Resize += (size) => ResizeSkiaSurface();

        WindowManager.AddWindow(_window);
    }
    
    public static void WaitTillAllWindowsClosed()
    {
        while (WindowManager.HasOpenWindows)
        {
            Task.Delay(100);
        }
    }

    private void OnLoad()
    {
        _gl = _window.CreateOpenGL();

        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);
        uint framebuffer = 0;
        _gl.GetInteger(GLEnum.FramebufferBinding, framebuffer);
        _fbInfo = new GRGlFramebufferInfo(framebuffer, SKColorType.Rgba8888.ToGlSizedFormat());

        _gl.Enable(GLEnum.Blend);
        _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        ResizeSkiaSurface();
    }

    // Called also during resizing of the window
    private void ResizeSkiaSurface() {
        _surface?.Dispose();
        var w = _window.FramebufferSize.X;
        var h = _window.FramebufferSize.Y;
        _backendRenderTarget = new GRBackendRenderTarget(w, h, 0, 8, _fbInfo);
        _surface = SKSurface.Create(_grContext, _backendRenderTarget,
            GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }

    private void OnRender(double delta) {
        if (_surface == null || _grContext == null || _window.IsClosing) return;

        if (IsInstantDraw)
            _drawBuffer.DequeueInto(_drawQueue);

        var canvas = _surface.Canvas;

        if (_clearColor.A >= 1) {
            canvas.Clear(_clearColor.ToSkia());
        } else {
            canvas.Clear(new SKColor(0, 0, 0, 0));
            // canvas.DrawRect(0, 0, _window.FramebufferSize.X, _window.FramebufferSize.Y, new SKPaint
            // {
            //     Color = _clearColor
            // });
        }

        Matrix3x3 automaticMatrix;
        Vector2 offset = (Vector2)_window.Size.ToVector2() * Origin;
        Matrix3x3 flipXMatrix = Matrix3x3.CreateScale(FlipX ? -1 : 1, FlipY ? -1 : 1);
        if (ScaleCanvasWithWindow) {
            var scale = (Vector2)_window.Size.ToVector2() / _canvaseScaleOriginalSize;
            offset /= scale;
            automaticMatrix = Matrix3x3.CreateScale(scale.X, scale.Y) * Matrix3x3.CreateTranslation(offset.X, offset.Y) * flipXMatrix;
        } else {
            automaticMatrix = Matrix3x3.CreateTranslation(offset.X, offset.Y) * flipXMatrix;
        }

        canvas.SetMatrix((WindowMatrix * automaticMatrix).ToSkia());

        _drawQueue.Draw(canvas);

        canvas.Flush();
        _grContext.Flush();
        canvas.ResetMatrix();
        _drawnNextFrame = true;
    }

    public void Show() { //TODO will cause the "Clear" from other windows to render? maybe even more...
        _drawBuffer.DequeueInto(_drawQueue);
        _drawnNextFrame = false;
        while (!_drawnNextFrame) {
            Task.Delay(1);
        }
    }

    public void Clear(Color? clearColor = null) {
        _drawQueue.Clear();
        if (clearColor == null) _clearColor = Color.BLACK;
        else _clearColor = clearColor;
    }

    private void OnClose()
    {
        _surface?.Dispose();
        _backendRenderTarget?.Dispose();
        _grContext?.Dispose();
    }


    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int LWA_ALPHA = 0x2;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private static void MakeWindowTransparent(IntPtr hwnd, byte opacity = 128, bool clickThrough = false)
    {
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        extendedStyle |= WS_EX_LAYERED;
        if (clickThrough)
            extendedStyle |= WS_EX_TRANSPARENT;

        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
        SetLayeredWindowAttributes(hwnd, 0, opacity, LWA_ALPHA);
    }

    public void MakeWindowTransparent(byte opacity = 128, bool clickThrough = false)
    {
        Glfw.GetApi();
        if (_window.Native?.Win32 == null) {
            Console.WriteLine("MakeWindowTransparent: Window is null!");
            return;
        }
        if (_window.Native.Win32.Value.Hwnd == IntPtr.Zero){
            Console.WriteLine("MakeWindowTransparent: Hwnd is null!");
            return;
        }
        MakeWindowTransparent(_window.Native.Win32.Value.Hwnd, opacity, clickThrough);
    }
}
