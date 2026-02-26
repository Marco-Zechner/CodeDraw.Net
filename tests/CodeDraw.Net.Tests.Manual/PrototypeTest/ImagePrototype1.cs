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

    // Pan = top-left of WINDOW in LAYER coords (pixels in your imageLayer)
    private Vector2 _pan = Vector2.Zero;

    private bool _panning;
    private Vector2<double> _panStartMouse;
    private Vector2 _panStartPan;

    [ConstructorPrototype("ImagePrototype1")]
    public ImagePrototype1()
    {
        using var app = CodeDrawHost.Start();

        _mainWindow = new CodeDrawWindow(1024, 1024, 100, 100, "Main");
        var clientWindow = new CodeDrawWindow(600, 600, 1150, 120, "Client");

        var img = CodeDrawImage.CsProject("easy.png", "PrototypeTest/images");

        // Render target (the "world" both windows will view)
        var imageLayer = new CodeDrawLayer(img.Width, img.Height, "ImageLayer");

        // Present the SAME layer in BOTH windows
        _mainWindow.SetPresentedLayer(imageLayer);
        clientWindow.SetPresentedLayer(imageLayer);

        // Camera mode for both
        _mainWindow.Settings = _mainWindow.Settings with { PresentMode = WindowPresentMode.Camera };
        clientWindow.Settings = clientWindow.Settings with { PresentMode = WindowPresentMode.Camera };

        // Pick background colors (outside of image bounds)
        _mainWindow.Settings = _mainWindow.Settings with { BackgroundColor = new ColorF(0.08f, 0.09f, 0.10f, 1f) };
        clientWindow.Settings = clientWindow.Settings with { BackgroundColor = new ColorF(0.06f, 0.06f, 0.07f, 1f) };

        // Keep camera behavior deterministic
        _mainWindow.Camera.ResizePolicy = CameraResizePolicy.Manual;
        clientWindow.Camera.ResizePolicy = CameraResizePolicy.Manual;

        // Input hooks
        app.Input.OnMouseDown += OnMouseDown;
        app.Input.OnMouseUp += OnMouseUp;
        app.Input.OnMouseMove += OnMouseMove;

        // Render & update cameras
        while (app.WindowsAlive > 0)
        {
            // Ensure imageLayer is exactly the image size (if image can change later)
            imageLayer.RequestLayerSize(img.Width, img.Height);

            // Draw the image into the imageLayer (world)
            imageLayer.Clear(0, 0, 0, 0); // transparent so window background shows outside
            imageLayer.DrawImage(img, imageLayer.FullRect);
            imageLayer.DrawDebugRect(img.Width/4, img.Height/4, img.Width/2, img.Height/2, 1, 0, 0, 0.5f); // outline for debugging
            imageLayer.Render();

            // Update camera transforms:
            // WindowToLayer should translate window pixel coords into layer coords.
            // If _pan = (layerX, layerY) that the window's top-left should map to,
            // then WindowToLayer is a translation by +pan.
            ApplyPan(_mainWindow, _pan);

            // For now client shows same view; later you’ll set it to a separate rectangle view.
            ApplyPan(clientWindow, _pan);

            // Let windows present (their own threads)
            Thread.Sleep(16);
        }

        app.Input.OnMouseDown -= OnMouseDown;
        app.Input.OnMouseUp -= OnMouseUp;
        app.Input.OnMouseMove -= OnMouseMove;

        app.WaitForClose();
    }

    private static void ApplyPan(CodeDrawWindow w, Vector2 panLayerPx)
    {
        // Use the camera's param mode (matches how PresentLoop expects uWindowToLayer)
        w.Camera.UseParams();
        w.Camera.ResizePolicy = CameraResizePolicy.Manual;

        // 1:1 view: one window pixel corresponds to one layer pixel
        var cs = w.Settings.Size; // client size snapshot
        w.Camera.ViewSizeLayer = new Vector2(cs.X, cs.Y);

        // Pan = where the window's top-left maps into layer-space.
        // In your Rebuild convention, PositionLocal is applied AFTER scaling and pivot shift.
        // So set it directly and rebuild.
        w.Camera.PositionGlobal = Vector2.Zero;
        w.Camera.PositionLocal  = panLayerPx;
        w.Camera.RotationDegCw  = 0f;

        w.Camera.Rebuild(cs.X, cs.Y);

        // Optional: debug once in a while (printing every frame will murder perf)
        // Console.WriteLine($"ApplyPan: pan={panLayerPx} w2l={w.Camera.WindowToLayer}");
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

        // Dragging right should reveal "more left side" of the image (i.e. pan decreases).
        // In screen coords: moving mouse +X means we want pan -X.
        _pan = _panStartPan - new Vector2((float)d.X, (float)d.Y);
    }

    private void OnMouseUp(CodeDrawWindow window, MouseButton button, ModifierKeys modifiers)
    {
        if (window != _mainWindow) return;

        if (button == MouseButton.Right)
            _panning = false;
    }
}