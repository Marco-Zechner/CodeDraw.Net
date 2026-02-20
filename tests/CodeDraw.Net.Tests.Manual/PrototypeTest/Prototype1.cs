using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Window;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public sealed class Prototype1
{
    private float _tA;
    private float _tOverlay;

    [ConstructorPrototype(1)]
    public Prototype1()
    {
        using var app = CodeDrawHost.Started();
        
        
        var winCombined = new CodeDrawWindow(800, 500, "Combined");
        var winLayerA = new CodeDrawWindow(800, 500, "LayerA");
        var winLayerB = new CodeDrawWindow(800, 500, "LayerB");

        // var winCombined2 = new CodeDrawWindow(800, 500, "Combined");
        // winCombined2.SetPresentedLayer(winCombined.Layer);
        winLayerA.OnStart = w => Console.WriteLine($"A started (id={w.WindowId})");
        winLayerB.OnStart = w => Console.WriteLine($"B started (id={w.WindowId})");
        winCombined.OnStart = w => Console.WriteLine($"Combined started (id={w.WindowId})");
        winLayerA.OnClose = w => Console.WriteLine($"A closed (id={w.WindowId})");
        winLayerB.OnClose = w => Console.WriteLine($"B closed (id={w.WindowId})");
        winCombined.OnClose = w => Console.WriteLine($"Combined closed (id={w.WindowId})");

        app.Input.OnKeyDown += ((win, key, _) =>
        {
            switch (key)
            {
                case Keys.Escape:
                    win.Close();
                    break;
                case Keys.F11:
                    win.Settings = win.Settings with { State = win.Settings.State == WindowState.Windowed ? WindowState.BorderlessFullscreen : WindowState.Windowed };
                    break;
            }
        });
        
        winLayerA.OnClose += window => window.Dispose(); 

        winLayerA.OnUpdate = ctx =>
        {
            _tA += ctx.DeltaSeconds;

            var layer = ctx.Win.Layer;
            if (layer.IsDisposed) return;

            layer.RequestLayerSize(800, 500);
            layer.Clear();

            layer.DrawRect(60 + 120 * MathF.Sin(_tA), 80, 220, 140, 0.2f, 1.0f, 0.6f, 1f);
            layer.DrawRect(90, 260, 140, 80, 1.0f, 0.3f, 0.2f, 0.8f);

            layer.SetBlendMode(BlendMode.NONE);
            layer.DrawRect(230, 5, 300, 40, 0.2f, 0.4f, 1.0f, 0.5f + 0.5f * MathF.Sin(_tOverlay * 2f));
            layer.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);

            layer.Render();
        };

        winLayerB.UpdateDelayMs = 33;
        winLayerB.OnUpdate = ctx =>
        {
            _tOverlay += ctx.DeltaSeconds;

            var layer = ctx.Win.Layer;
            if (layer.IsDisposed) return;

            layer.RequestLayerSize(800, 500);
            layer.Clear();
            layer.DrawRect(10, 5, 240, 40, 0.8f, 0.2f, 0.5f, 0.5f + 0.5f * MathF.Sin(_tOverlay * 2f));
            layer.DrawRect(400 + 100 * MathF.Sin(_tOverlay * 2f), 250 + 100 * MathF.Cos(_tOverlay * 2f), 20 + 10 * MathF.Cos(_tOverlay * 5f), 20 + 10 * MathF.Sin(_tOverlay * 5f), 0.8f, 0.2f, 0.5f, 1f);
            layer.Render();
        };

        winCombined.OnUpdate = ctx =>
        {
            var layer = ctx.Win.Layer;
            if (layer.IsDisposed) return;

            layer.RequestLayerSize(800, 500);
            layer.Clear(0.05f, 0.05f, 0.05f, 1f);

            if (winLayerA is { IsDisposed: false, ShouldClose: false, Layer.IsDisposed: false })
            {
                layer.DrawLayer(winLayerA.Layer);
            }

            if (winLayerB is { IsDisposed: false, ShouldClose: false, Layer.IsDisposed: false })
            {
                layer.DrawLayer(winLayerB.Layer);
            }

            layer.Render();
        };
        
        app.WaitForClose();
    }
}
