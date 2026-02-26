using System.Diagnostics;
using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.CodeDrawDotNet.Text;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using MarcoZechner.ColorDotNet.RGB;
using MouseButton = Silk.NET.GLFW.MouseButton;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public class Prototype4
{
    private readonly List<CodeDrawWindow> _windows = [];
    private readonly CodeDrawLayer _fullMonitorLayer;

    private readonly Vector2<int> _worldOrigin; // monitor work-area top-left
    private readonly Vector2<int> _worldSize;   // monitor work-area size
    
    [ConstructorPrototype(4)]
    public Prototype4()
    {      
        using var app = CodeDrawHost.Start();
        
        var worldMonitor = app.GetMonitors().First();
        _worldOrigin = new Vector2<int>(worldMonitor.WorkX, worldMonitor.WorkY);
        _worldSize   = new Vector2<int>(worldMonitor.WorkWidth, worldMonitor.WorkHeight);

        _fullMonitorLayer = new CodeDrawLayer(_worldSize.X, _worldSize.Y, "FullMonitorLayer");
        var orbitShader = CodeDrawShader.CsProject("orbitDots", "PrototypeTest/shaders");
        var postProcessingBloom = CodeDrawShader.CsProject("bloom" , "PrototypeTest/shaders/ppShader");
        
        var trailLayer = new CodeDrawLayer(468, 468, "TrailLayer");

        app.Input.OnKeyDown += (window, key, _) =>
        {
            Console.WriteLine(key.ToString());
            
            switch (key)
            {
                case Keys.RightBracket: CreateNextWindow();
                    break;
                case Keys.H: window.ToggleFrameMode();
                    break;
            }
        };
        
        app.Input.OnKeyRepeat += (window, key, _) =>
        {
            var delta = Vector2<int>.Zero;
            
            switch (key)
            {
                case Keys.Left: delta = delta with { X = delta.X - 10 };
                    break;
                case Keys.Right: delta = delta with { X = delta.X + 10 };
                    break;
                case Keys.Up: delta = delta with { Y = delta.Y - 10 };
                    break;
                case Keys.Down: delta = delta with { Y = delta.Y + 10 };
                    break;
            }
            
            window.WindowPosition += delta;
        };

        Vector2<double> mouseWindowOffset = Vector2<double>.Zero;
        bool dragging = false;

        app.Input.OnMouseDown += (win, button, _) =>
        {
            if (button != MouseButton.Left) return;
            if (!win.Input.GetKey(ModifierKeys.ALT)) return;

            mouseWindowOffset = new Vector2<double>(win.Input.MouseX, win.Input.MouseY);
            dragging = true;
        };

        app.Input.OnMouseUp += (_, button, _) =>
        {
            if (button == MouseButton.Left)
                dragging = false;
        };

        app.Input.OnKeyUp += (_, key, _) =>
        {
            if (ModifierKeys.ALT.ToKeys().Contains(key)) 
                dragging = false;
        };

        app.Input.OnMouseMove += (win, _, _) =>
        {
            if (!dragging) return;

            var mouseGlobal = app.Input.GetAbsoluteMousePosition();

            var delta = mouseGlobal - mouseWindowOffset;
            
            // Move relative to the window position when drag started
            win.WindowPosition = new Vector2<int>((int)Math.Floor(delta.X), (int)Math.Floor(delta.Y));
        };

        CreateNextWindow();

        var sw = Stopwatch.StartNew();

        const int GRID_STEP = 80;
        const int GRID_THIN = 1;
        const int GRID_THICK_EVERY = 5; // every 5th line thicker

        while (app.WindowsAlive > 0)
        {
            var t = (float)sw.Elapsed.TotalSeconds;

            var layer = _fullMonitorLayer;
            if (layer.IsDisposed)
                break;

            // Ensure the shared "world" texture matches the monitor work-area
            layer.RequestLayerSize(_worldSize.X, _worldSize.Y);

            // --- Background ---
            layer.Clear(0.05f, 0.06f, 0.07f, 1f);

            // --- Grid (thin + thick) ---

            for (var x = 0; x <= _worldSize.X; x += GRID_STEP)
            {
                var thick = ((x / GRID_STEP) % GRID_THICK_EVERY) == 0;
                var a = thick ? 0.55f : 0.18f;
                float w = thick ? 3 : GRID_THIN;
                layer.DrawDebugRect(x, 0, w, _worldSize.Y, 1f, 1f, 1f, a);
            }

            for (var y = 0; y <= _worldSize.Y; y += GRID_STEP)
            {
                var thick = ((y / GRID_STEP) % GRID_THICK_EVERY) == 0;
                var a = thick ? 0.55f : 0.18f;
                float h = thick ? 3 : GRID_THIN;
                layer.DrawDebugRect(0, y, _worldSize.X, h, 1f, 1f, 1f, a);
            }

            var style = new TextStyle() {
                Font = FontRef.FromFile(@"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf")
                    .WithVariant(FontVariant.Regular),
                Align = TextAlign.Center,
                VAlign = TextVAlign.Top,
                SizePx = 50,
            };
            layer.DrawText("Hello", layer.Width/2,40, style);

            // --- World origin marker (top-left of workarea) ---
            // Big L-corner + label-ish blocks
            layer.DrawDebugRect(0, 0, 140, 10, 1f, 0.2f, 0.2f, 1f);
            layer.DrawDebugRect(0, 0, 10, 140, 1f, 0.2f, 0.2f, 1f);
            layer.DrawDebugRect(16, 16, 18, 18, 1f, 0.2f, 0.2f, 1f);
            layer.DrawDebugRect(40, 16, 18, 18, 1f, 0.2f, 0.2f, 1f);

            // --- Center crosshair of the whole world ---
            var cx = _worldSize.X * 0.5f;
            var cy = _worldSize.Y * 0.5f;
            layer.DrawDebugRect(cx - 140, cy - 3, 280, 6, 0.2f, 0.85f, 0.25f, 0.95f);
            layer.DrawDebugRect(cx - 3, cy - 140, 6, 280, 0.2f, 0.85f, 0.25f, 0.95f);

            // --- Moving "comet" (makes it obvious you're seeing live updates) ---
            var mx = cx + 0.35f * _worldSize.X * MathF.Sin(t * 0.7f) + 0.12f * _worldSize.X * MathF.Cos(t * 1.4f);
            var my = cy + 0.25f * _worldSize.Y * MathF.Cos(t * 0.9f) + 0.10f * _worldSize.Y * MathF.Sin(t * 1.9f);

            // tail
            for (var i = 0; i < 18; i++)
            {
                var k = i / 18f;
                var tx = mx - 220f * k;
                var ty = my - 120f * k;
                var a = (1f - k) * 0.35f;
                layer.DrawDebugRect(tx - 4, ty - 4, 8, 8, 0.9f, 0.9f, 1f, a);
            }

            // head
            layer.DrawDebugRect(mx - 10, my - 10, 20, 20, 0f, 0f, 0f, 1f);
            layer.DrawDebugRect(mx - 7, my - 7, 14, 14, 1f, 1f, 1f, 1f);

            // --- Orbiting dots around world center (simple, no shader) ---

            trailLayer.DrawDebugRect(0,0, trailLayer.Width, trailLayer.Height, 0f,0f,0f, 0.005f); // fade old frames
            DrawOrbitDots(trailLayer, orbitShader, trailLayer.Width/2, trailLayer.Height/2, 14, 220, 6f, 0, new ColorF(1.00f, 0.45f, 0.10f, 1.00f));
            trailLayer.Render();
            layer.SetBlendMode(BlendMode.ADD);
            layer.DrawLayer(trailLayer, dstRect: new RectWh(cx-trailLayer.Width/2f, cy-trailLayer.Height/2f, trailLayer.Width, trailLayer.Height));
            layer.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);
            
            DrawOrbitDots(layer, orbitShader, (int)cx, (int)cy, 10,360, -4f, 0, new ColorF(0.10f, 0.80f, 1.00f, 1.00f));
            DrawOrbitDots(layer, orbitShader, (int)cx, (int)cy, 10,360, -4f, 1, new ColorF(0.10f, 0.80f, 1.00f, 1.00f));
            DrawOrbitDots(layer, orbitShader, (int)cx, (int)cy, 8, 520, 25f, 0, new ColorF(0.80f, 0.20f, 1.00f, 1.00f));

            var glow = 25 + 25 * MathF.Sin(t * 5f);
            
            layer.PostProcess(postProcessingBloom,
                uniforms: Uniforms.Of(
                    UniformValue.Float("uGlow", glow)
                )
            );
            
            // Publish to GPU texture so all windows can pick it up
            layer.Render();

            // Keep CPU sane (present threads run independently)
            Thread.Sleep(16);
        }
        
        app.WaitForClose();
    }

    private static void DrawOrbitDots(CodeDrawLayer layer, CodeDrawShader orbitShader, int centerX, int centerY, int radiusDot, int radiusOrbit, float period, float timeOffset, ColorF color)
    {
        var size = radiusOrbit * 2 + radiusDot * 2;
        layer.DrawCustomRect(
            new RectWh<int>(centerX - size / 2, centerY - size / 2, size, size),
            shader: orbitShader,
            uniforms: Uniforms.Of(
                UniformValue.Float("uTime", layer.TimeAliveSeconds),
                UniformValue.Float4("uColor", color.R, color.G, color.B, color.A),
                UniformValue.Float("uRadius1", radiusDot),
                UniformValue.Float("uRadius2", radiusOrbit),
                UniformValue.Float("uPeriod",  period),
                UniformValue.Float("uOffset",  timeOffset)
            )
        );
    }

    private void CreateNextWindow()
    {
        var win = new CodeDrawWindow(1200, 1200, _fullMonitorLayer.Width/2-600, _fullMonitorLayer.Height/2-600, $"Prototype4 - {_windows.Count}");
        _windows.Add(win);
        
        win.SetPresentedLayer(_fullMonitorLayer);
        
        win.Settings = win.Settings with
        {
            PresentMode = WindowPresentMode.Camera,
            BackgroundColor = Colors.TRANSPARENT, 
        };
        
        win.Camera.ResizePolicy = CameraResizePolicy.Manual;
        
        win.OnUpdate += context =>
        {
            var w = context.Win;

            // Window's OUTER top-left in screen coords (work-area coords are also screen coords)
            var winPosOuter = w.WindowPosition; // Vector2<int>
            var winClient   = w.Size;           // Vector2<int> CLIENT size in your Settings getter

            // Convert to world coords (relative to the monitor work-area origin)
            var topLeftWorld = new Vector2(
                winPosOuter.X - _worldOrigin.X,
                winPosOuter.Y - _worldOrigin.Y
            );

            topLeftWorld = ClampTopLeftToWorld(topLeftWorld, winClient, _worldSize);

            w.Camera.WindowToLayer = Matrix3x3.CreateTranslation(topLeftWorld.X, topLeftWorld.Y);
        };
    }
    
    private static Vector2 ClampTopLeftToWorld(Vector2 topLeftWorld, Vector2<int> winClient, Vector2<int> worldSize)
    {
        // If window is larger than world, allow negative to center-ish? For now clamp hard.
        float maxX = Math.Max(0, worldSize.X - winClient.X);
        float maxY = Math.Max(0, worldSize.Y - winClient.Y);

        var x = Math.Clamp(topLeftWorld.X, 0, maxX);
        var y = Math.Clamp(topLeftWorld.Y, 0, maxY);
        return new Vector2(x, y);
    }
}