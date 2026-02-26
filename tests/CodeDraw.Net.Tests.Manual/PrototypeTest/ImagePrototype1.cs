using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Images;
using MarcoZechner.CodeDrawDotNet.Text;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;
using MouseButton = Silk.NET.GLFW.MouseButton;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public sealed class ImagePrototype1
{
    private readonly CodeDrawWindow _mainWindow;
    private readonly CodeDrawWindow _clientWindow;
    private readonly CodeDrawLayer _imageLayer;
    private readonly CodeDrawImage _img;

    // Main view
    private Vector2 _panMain = Vector2.Zero;
    private float _zoomMain = 1f; // 1 = 1:1

    // Client view
    private Vector2 _panClient = Vector2.Zero;
    private float _zoomClient = 1f;

    private bool _panningMainRmb;
    private Vector2<double> _panStartMouseMain;
    private Vector2 _panStartPanMain;

    private bool _panningClientCtrlLmb;
    private Vector2<double> _panStartMouseClient;
    private Vector2 _panStartPanClient;
    
    private bool _zoomBurstActive;
    private long _lastScrollTick;
    private Vector2 _zoomAnchorWinPx; // in MAIN window pixel coords
    private const long ZOOM_BURST_TICKS = TimeSpan.TicksPerMillisecond * 120;

    [ConstructorPrototype("ImagePrototype1")]
    public ImagePrototype1()
    {
        using var app = CodeDrawHost.Start();

        _mainWindow   = new CodeDrawWindow(1024, 1024, 100, 100, "Main");
        _clientWindow = new CodeDrawWindow(600,  600,  1150, 120, "Client");

        _img = CodeDrawImage.CsProject("easy.png", "PrototypeTest/images");

        _imageLayer = new CodeDrawLayer(_img.Width, _img.Height, "ImageLayer");

        _mainWindow.SetPresentedLayer(_imageLayer);
        _clientWindow.SetPresentedLayer(_imageLayer);

        _mainWindow.Settings   = _mainWindow.Settings   with { PresentMode = WindowPresentMode.Camera, BackgroundColor = new ColorF(0.08f, 0.09f, 0.10f, 1f) };
        _clientWindow.Settings = _clientWindow.Settings with { PresentMode = WindowPresentMode.Camera, BackgroundColor = new ColorF(0.06f, 0.06f, 0.07f, 1f) };

        _mainWindow.Camera.ResizePolicy = CameraResizePolicy.Manual;
        _clientWindow.Camera.ResizePolicy = CameraResizePolicy.Manual;

        // Input hooks
        app.Input.OnMouseDown += OnMouseDown;
        app.Input.OnMouseUp   += OnMouseUp;
        app.Input.OnMouseMove += OnMouseMove;
        app.Input.OnScroll += OnScroll;

        var ups = 0f;
        var updates = 0;
        var upsAccum = 0f;

        var baseFont = FontRef.FromFile(
            @"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf"
        );
        
        var styleHud = new TextStyle
        {
            Font = baseFont.WithVariant(FontVariant.Regular),
            SizePx = 30,
            VAlign = TextVAlign.Top,
            Align = TextAlign.Left,
            Color = new ColorF(1f, 1f, 1f, 0.95f),
            Background = new ColorF(0f, 0f, 0f, 0.55f),
            DebugMode = TextDebugMode.None,
            DebugRects = DebugRectMode.Outline,
            DebugOutlinePx = 1,
            MonospaceSnapLineAlignToCells = true
        };
        
        _mainWindow.OnUpdate += ctx =>
        {
            updates++;
            upsAccum += ctx.DeltaSeconds;
            if (upsAccum >= 0.25f)
            {
                ups = updates / upsAccum;
                updates = 0;
                upsAccum = 0f;
            }

            _imageLayer.RequestLayerSize(_img.Width, _img.Height);

            _imageLayer.Clear(0, 0, 0, 0);
            _imageLayer.DrawImage(_img, _imageLayer.FullRect);

            ApplyCamera(_mainWindow, _panMain, _zoomMain);
            ApplyCamera(_clientWindow, _panClient, _zoomClient);

            // visualize client window bounds in layer-space
            var topLeft = _clientWindow.Camera.WindowToLayerPoint(Vector2.Zero);
            var bottomRight = _clientWindow.Camera.WindowToLayerPoint(new Vector2(_clientWindow.Width, _clientWindow.Height));

            _imageLayer.DrawDebugRect(topLeft.X - 10, topLeft.Y, 10, bottomRight.Y - topLeft.Y, 1, 0, 0, 0.5f);
            _imageLayer.DrawDebugRect(bottomRight.X, topLeft.Y, 10, bottomRight.Y - topLeft.Y, 1, 0, 0, 0.5f);
            _imageLayer.DrawDebugRect(topLeft.X, topLeft.Y - 10, bottomRight.X - topLeft.X, 10, 1, 0, 0, 0.5f);
            _imageLayer.DrawDebugRect(topLeft.X, bottomRight.Y, bottomRight.X - topLeft.X, 10, 1, 0, 0, 0.5f);

            if (_zoomBurstActive && (DateTime.UtcNow.Ticks - _lastScrollTick) > ZOOM_BURST_TICKS)
                _zoomBurstActive = false;
            
            var mainTopLeft = _mainWindow.Camera.WindowToLayerPoint(Vector2.Zero);
            
            //TODO: add a "HUD_LAYER" to windows?
            var hud = $"UPS: {ups:0.0}";
            var hudSize = _imageLayer.MeasureText(hud, styleHud);
            _imageLayer.DrawText(hud, mainTopLeft.X + 20, mainTopLeft.Y + 20, styleHud);
            
            _imageLayer.Render();
            Thread.Sleep(16);
        };
        
        app.WaitForClose();

        app.Input.OnMouseDown -= OnMouseDown;
        app.Input.OnMouseUp   -= OnMouseUp;
        app.Input.OnMouseMove -= OnMouseMove;
        app.Input.OnScroll -= OnScroll;
    }
   // windowPx -> layerPx: layer = pan + window/zoom
    private static void ApplyCamera(CodeDrawWindow w, Vector2 panLayerPx, float zoom)
    {
        var z = zoom;
        if (z < 0.01f) z = 0.01f;

        var s = Matrix3x3.CreateScale(1f / z, 1f / z);
        var t = Matrix3x3.CreateTranslation(panLayerPx.X, panLayerPx.Y);
        w.Camera.WindowToLayer = t * s;
    }

    private void OnMouseDown(CodeDrawWindow window, MouseButton button, ModifierKeys modifiers)
    {
        // ignore input on client window entirely
        if (window != _mainWindow) return;

        if (button == MouseButton.Right)
        {
            _panningMainRmb = true;
            _panStartMouseMain = window.Input.MousePos;
            _panStartPanMain = _panMain;
            return;
        }

        // Ctrl + LMB pans CLIENT view (but gesture begins on MAIN window)
        if (button == MouseButton.Left && modifiers.HasFlag(ModifierKeys.CONTROL))
        {
            _panningClientCtrlLmb = true;
            _panStartMouseClient = -window.Input.MousePos;
            _panStartPanClient = _panClient;
        }
    }

    private void OnMouseMove(CodeDrawWindow window, double deltaX, double deltaY)
    {
        // ignore input on client window entirely
        if (window != _mainWindow) return;

        if (_panningMainRmb)
        {
            var mouse = window.Input.MousePos;
            var d = mouse - _panStartMouseMain;
            _panMain = _panStartPanMain - new Vector2((float)d.X, (float)d.Y) * (1f / _zoomMain);
            return;
        }

        if (_panningClientCtrlLmb)
        {
            var mouse = -window.Input.MousePos;
            var d = mouse - _panStartMouseClient;
            _panClient = _panStartPanClient - new Vector2((float)d.X, (float)d.Y) * (1f / _zoomClient);
        }
    }

    private void OnMouseUp(CodeDrawWindow window, MouseButton button, ModifierKeys modifiers)
    {
        // ignore input on client window entirely
        if (window != _mainWindow) return;

        if (button == MouseButton.Right)
            _panningMainRmb = false;

        if (button == MouseButton.Left)
            _panningClientCtrlLmb = false;
    }

    // RMB + scroll = zoom main
    // Ctrl + LMB + scroll = zoom client
    private void OnScroll(CodeDrawWindow window, double scrollX, double scrollY)
    {
        // ignore scroll on client window entirely
        if (window != _mainWindow) return;
        if (scrollY == 0) return;

        const float ZOOM_STEP = 1.12f;

        // Capture a stable anchor during a "scroll burst"
        _lastScrollTick = DateTime.UtcNow.Ticks;
        if (!_zoomBurstActive)
        {
            _zoomBurstActive = true;
            var m = _mainWindow.Input.MousePos;
            _zoomAnchorWinPx = new Vector2((float)m.X, (float)m.Y);
        }

        if (_panningMainRmb)
        {
            ZoomAtAnchorWinPx(ref _panMain, ref _zoomMain, scrollY, ZOOM_STEP, _zoomAnchorWinPx);
            return;
        }

        if (_panningClientCtrlLmb)
        {
            ZoomAtAnchorWinPx(ref _panClient, ref _zoomClient, scrollY, ZOOM_STEP, _zoomAnchorWinPx); //TODO: why inverted needed
        }
    }

    // Zoom around a fixed window-space anchor so zoom doesn't "chase" a moving mouse.
    // anchorWinPx is in window px of the MAIN window.
    private static void ZoomAtAnchorWinPx(ref Vector2 pan, ref float zoom, double scrollY, float zoomStep, Vector2 anchorWinPx)
    {
        var oldZ = zoom;
        if (oldZ < 0.01f) oldZ = 0.01f;

        // layer under anchor BEFORE: layer = pan + anchor/zoom
        var layerUnderAnchor = pan + anchorWinPx * (1f / oldZ);

        // Update zoom
        if (scrollY > 0) zoom *= zoomStep;
        else zoom /= zoomStep;

        if (zoom < 0.01f) zoom = 0.01f;

        // pan' = layerUnderAnchor - anchor/zoom'
        pan = layerUnderAnchor - anchorWinPx * (1f / zoom);
    }
}