using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Images;
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

    private Vector2 _pan = Vector2.Zero;

    private bool _panning;
    private Vector2<double> _panStartMouse;
    private Vector2 _panStartPan;

    [ConstructorPrototype("ImagePrototype1")]
    public ImagePrototype1()
    {
        using var app = CodeDrawHost.Start();

        _mainWindow   = new CodeDrawWindow(1024, 1024, 100, 100, "Main");
        _clientWindow = new CodeDrawWindow(600,  600,  1150, 120, "Client");

        _img = CodeDrawImage.CsProject("easy.png", "PrototypeTest/images");

        // World = exactly the image size (so outside = background color in camera present)
        _imageLayer = new CodeDrawLayer(_img.Width, _img.Height, "ImageLayer");

        _mainWindow.SetPresentedLayer(_imageLayer);
        _clientWindow.SetPresentedLayer(_imageLayer);

        _mainWindow.Settings   = _mainWindow.Settings   with { PresentMode = WindowPresentMode.Camera, BackgroundColor = new ColorF(0.08f, 0.09f, 0.10f, 1f) };
        _clientWindow.Settings = _clientWindow.Settings with { PresentMode = WindowPresentMode.Camera, BackgroundColor = new ColorF(0.06f, 0.06f, 0.07f, 1f) };

        // We will drive WindowToLayer ourselves. No param mode.
        _mainWindow.Camera.ResizePolicy = CameraResizePolicy.Manual;
        _clientWindow.Camera.ResizePolicy = CameraResizePolicy.Manual;

        // Input hooks
        app.Input.OnMouseDown += OnMouseDown;
        app.Input.OnMouseUp   += OnMouseUp;
        app.Input.OnMouseMove += OnMouseMove;

        // Render loop (CPU side)
        while (app.WindowsAlive > 0)
        {
            // (Optional) keep in sync if image meta becomes known later / hot reload
            _imageLayer.RequestLayerSize(_img.Width, _img.Height);

            _imageLayer.Clear(0, 0, 0, 0); // transparent; outside image shows window background
            _imageLayer.DrawImage(_img, _imageLayer.FullRect);

            // Apply camera matrices (just translation)
            ApplyPan(_clientWindow, _pan);
            
            var topLeft = _clientWindow.Camera.WindowToLayerPoint(Vector2.Zero);
            var bottomRight = _clientWindow.Camera.WindowToLayerPoint(new Vector2(_clientWindow.Size.X, _clientWindow.Size.Y));
            
            _imageLayer.DrawDebugRect(topLeft.X, topLeft.Y, 20, 20, 1, 0, 0, 0.5f);
            
            ApplyPan(_mainWindow, _pan);


            _imageLayer.Render();
            Thread.Sleep(16);
        }

        app.Input.OnMouseDown -= OnMouseDown;
        app.Input.OnMouseUp   -= OnMouseUp;
        app.Input.OnMouseMove -= OnMouseMove;

        app.WaitForClose();
    }

    private static void ApplyPan(CodeDrawWindow w, Vector2 panLayerPx)
    {
        // 1:1 mapping window px -> layer px, with a translation.
        // Window pixel (0,0) samples layer pixel (pan.x, pan.y).
        w.Camera.WindowToLayer = Matrix3x3.CreateTranslation(panLayerPx.X, panLayerPx.Y);
    }

    private void OnMouseDown(CodeDrawWindow window, MouseButton button, ModifierKeys modifiers)
    {
        if (window != _mainWindow) return;

        if (button == MouseButton.Right)
        {
            _panning = true;
            _panStartMouse = window.Input.MousePos;
            _panStartPan = _pan;
        }
    }

    private void OnMouseMove(CodeDrawWindow window, double deltaX, double deltaY)
    {
        if (window != _mainWindow) return;
        if (!_panning) return;

        var mouse = window.Input.MousePos;
        var d = mouse - _panStartMouse;

        // mouse right => pan left
        _pan = _panStartPan - new Vector2((float)d.X, (float)d.Y);
    }

    private void OnMouseUp(CodeDrawWindow window, MouseButton button, ModifierKeys modifiers)
    {
        if (window != _mainWindow) return;
        if (button == MouseButton.Right) _panning = false;
    }
}