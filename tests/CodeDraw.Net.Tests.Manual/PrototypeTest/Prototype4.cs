using System.Diagnostics;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.Input;
using MouseButton = Silk.NET.GLFW.MouseButton;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(4)]
public class Prototype4 : IDisposable
{
    private static SharedGlfwHost _host = null!;
    
    [StaticPrototype]
    public static void RunTest()
    {
        _host = SharedGlfwHost.Instance;
        _host.Start();

        using (new Prototype4())
        {
            _host.WaitUntilAllWindowsClosed();
        }

        _host.Stop();
        _host.Dispose();
    }
    
    public void Dispose()
    {
        foreach (var w in _windows)
        {
            w.Dispose();
        }
        
        _fullMonitorLayer.Dispose();
    }
    
    private readonly List<CodeDrawWindow> _windows = [];
    private CodeDrawLayer _fullMonitorLayer;

    private readonly SharedGlfwHost.MonitorInfo _worldMonitor;
    private readonly Vector2<int> _worldOrigin; // monitor work-area top-left
    private readonly Vector2<int> _worldSize;   // monitor work-area size
    
    private Prototype4()
    {
        _worldMonitor = _host.GetMonitors().First();
        _worldOrigin = new Vector2<int>(_worldMonitor.WorkX, _worldMonitor.WorkY);
        _worldSize   = new Vector2<int>(_worldMonitor.WorkWidth, _worldMonitor.WorkHeight);

        _fullMonitorLayer = new CodeDrawLayer(_host, _worldSize.X, _worldSize.Y, "FullMonitorLayer");
        var orbitShader = CustomShader.CsProject("orbitDots", "PrototypeTest/shaders");
        var postProcessingBloom = CustomShader.CsProject("bloom", "PrototypeTest/shaders/ppShader");

        _host.Input.OnKeyDown += (window, key, mod) =>
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
        
        _host.Input.OnKeyRepeat += (window, key, mod) =>
        {
            var delta = Vector2<int>.Zero;
            
            switch (key)
            {
                case Keys.Left: delta = delta.WithX(delta.X - 10);
                    break;
                case Keys.Right: delta = delta.WithX(delta.X + 10);
                    break;
                case Keys.Up: delta = delta.WithY(delta.Y - 10);
                    break;
                case Keys.Down: delta = delta.WithY(delta.Y + 10);
                    break;
            }
            
            window.WindowPosition += delta;
        };

        Vector2<double> mouseWindowOffset = Vector2<double>.Zero;
        bool dragging = false;

        _host.Input.OnMouseDown += (win, button, mods) =>
        {
            if (button != MouseButton.Left) return;
            if (!win.Input.GetKey(ModifierKeys.ALT)) return;

            mouseWindowOffset = new Vector2<double>(win.Input.MouseX, win.Input.MouseY);
            dragging = true;
        };

        _host.Input.OnMouseUp += (win, button, mods) =>
        {
            if (button == MouseButton.Left)
                dragging = false;
        };

        _host.Input.OnKeyUp += (window, key, mods) =>
        {
            if (ModifierKeys.ALT.ToKeys().Contains(key)) 
                dragging = false;
        };

        _host.Input.OnMouseMove += (win, x, y) =>
        {
            if (!dragging) return;

            var mouseGlobal = _host.Input.GetAbsoluteMousePosition();

            var delta = mouseGlobal - mouseWindowOffset;
            
            // Move relative to the window position when drag started
            win.WindowPosition = new Vector2<int>((int)Math.Floor(delta.X), (int)Math.Floor(delta.Y));
        };

        CreateNextWindow();

        var sw = Stopwatch.StartNew();

        const int GRID_STEP = 80;
        const int GRID_THIN = 1;
        const int GRID_THICK_EVERY = 5; // every 5th line thicker

        while (_host.WindowsAlive > 0)
        {
            var t = (float)sw.Elapsed.TotalSeconds;

            var layer = _fullMonitorLayer;
            if (layer.IsDisposed)
                break;

            // Ensure the shared "world" texture matches the monitor work-area
            layer.RequestLayerSize(_worldSize.X, _worldSize.Y);

            // --- Background ---
            layer.SetBlendMode(BlendMode.NONE);
            layer.Clear(0.05f, 0.06f, 0.07f, 1f);

            // --- Grid (thin + thick) ---
            layer.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);

            for (var x = 0; x <= _worldSize.X; x += GRID_STEP)
            {
                var thick = ((x / GRID_STEP) % GRID_THICK_EVERY) == 0;
                var a = thick ? 0.55f : 0.18f;
                float w = thick ? 3 : GRID_THIN;
                layer.DrawRect(x, 0, w, _worldSize.Y, 1f, 1f, 1f, a);
            }

            for (var y = 0; y <= _worldSize.Y; y += GRID_STEP)
            {
                var thick = ((y / GRID_STEP) % GRID_THICK_EVERY) == 0;
                var a = thick ? 0.55f : 0.18f;
                float h = thick ? 3 : GRID_THIN;
                layer.DrawRect(0, y, _worldSize.X, h, 1f, 1f, 1f, a);
            }

            // --- World origin marker (top-left of workarea) ---
            // Big L-corner + label-ish blocks
            layer.DrawRect(0, 0, 140, 10, 1f, 0.2f, 0.2f, 1f);
            layer.DrawRect(0, 0, 10, 140, 1f, 0.2f, 0.2f, 1f);
            layer.DrawRect(16, 16, 18, 18, 1f, 0.2f, 0.2f, 1f);
            layer.DrawRect(40, 16, 18, 18, 1f, 0.2f, 0.2f, 1f);

            // --- Center crosshair of the whole world ---
            var cx = _worldSize.X * 0.5f;
            var cy = _worldSize.Y * 0.5f;
            layer.DrawRect(cx - 140, cy - 3, 280, 6, 0.2f, 0.85f, 0.25f, 0.95f);
            layer.DrawRect(cx - 3, cy - 140, 6, 280, 0.2f, 0.85f, 0.25f, 0.95f);

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
                layer.DrawRect(tx - 4, ty - 4, 8, 8, 0.9f, 0.9f, 1f, a);
            }

            // head
            layer.DrawRect(mx - 10, my - 10, 20, 20, 0f, 0f, 0f, 1f);
            layer.DrawRect(mx - 7, my - 7, 14, 14, 1f, 1f, 1f, 1f);

            // --- Orbiting dots around world center (simple, no shader) ---
            DrawOrbitDots(layer, orbitShader, (int)cx, (int)cy, 14, 220, 6f, 0, new Rgba(1.00f, 0.45f, 0.10f, 1.00f));
            DrawOrbitDots(layer, orbitShader, (int)cx, (int)cy, 10,360, -4f, 0, new Rgba(0.10f, 0.80f, 1.00f, 1.00f));
            DrawOrbitDots(layer, orbitShader, (int)cx, (int)cy, 8, 520, 25f, 0, new Rgba(0.80f, 0.20f, 1.00f, 1.00f));

            float glow = 2 + 2 * MathF.Sin(t * 1f);
            Console.WriteLine("glow: " + glow);
            
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
    }

    private static void DrawOrbitDots(CodeDrawLayer layer, CustomShader orbitShader, int centerX, int centerY, int radiusDot, int radiusOrbit, float period, float timeOffset, Rgba color)
    {
        var size = radiusOrbit * 2 + radiusDot * 2;
        layer.CustomDrawRect(
            centerX - size / 2, centerY - size / 2, size, size,
            shader: orbitShader,
            uniforms: Uniforms.Of(
                UniformValue.Float("uTime", layer.LayerAliveForSeconds()),
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
        var win = new CodeDrawWindow(_host, 400, 400, 200, 200, $"Prototype4 - {_windows.Count}");
        _windows.Add(win);
        
        win.SetPresentedLayer(_fullMonitorLayer);
        
        win.Settings = win.Settings with
        {
            PresentMode = WindowPresentMode.Camera,
            BackgroundColor = Color.Transparent, 
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

            w.Camera.WindowToLayer = Matrix3X3.CreateTranslation(topLeftWorld.X, topLeftWorld.Y);
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