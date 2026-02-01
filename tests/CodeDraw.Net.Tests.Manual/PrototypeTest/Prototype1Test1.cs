using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(1)]
public sealed class Prototype1Test1 : IDisposable
{
    [StaticPrototype]
    public static void RunTest()
    {
        var host = SharedGlfwHost.Instance;
        host.Start();

        using (var session = new Prototype1Test1(host))
        {
            session.WaitForClose();
        }

        host.Stop();
    }

    private readonly CodeDrawWindow _winLayerA;
    private readonly CodeDrawWindow _winLayerB;
    private readonly CodeDrawWindow _winCombined;

    private float _tA;
    private float _tOverlay;

    public Prototype1Test1(SharedGlfwHost host)
    {
        _winLayerA = new CodeDrawWindow(host, 800, 500, "LayerA");
        _winCombined = new CodeDrawWindow(host, 800, 500, "Combined");
        _winLayerB = new CodeDrawWindow(host, 800, 500, "LayerB");

        _winLayerA.OnStart = w => Console.WriteLine($"A started (id={w.WindowId})");
        _winLayerB.OnStart = w => Console.WriteLine($"B started (id={w.WindowId})");
        _winCombined.OnStart = w => Console.WriteLine($"Combined started (id={w.WindowId})");
        _winLayerA.OnClose = w => Console.WriteLine($"A closed (id={w.WindowId})");
        _winLayerB.OnClose = w => Console.WriteLine($"B closed (id={w.WindowId})");
        _winCombined.OnClose = w => Console.WriteLine($"Combined closed (id={w.WindowId})");

        host.Input.OnKeyDown += ((win, key, mods) =>
        {
            switch (key)
            {
                case Keys.Escape:
                    win.Close();
                    break;
                case Keys.F11:
                    win.MaximizeBorderless = !win.MaximizeBorderless;
                    break;
            }
        });

        _winLayerA.OnUpdate = ctx =>
        {
            _tA += ctx.DeltaSeconds;

            var layer = ctx.Win.Layer;
            if (layer is null || layer.IsDisposed) return;

            layer.EnsureCanvas(800, 500);
            layer.Clear();

            layer.DrawRect(60 + 120 * MathF.Sin(_tA), 80, 220, 140, 0.2f, 1.0f, 0.6f, 1f);
            layer.DrawRect(90, 260, 140, 80, 1.0f, 0.3f, 0.2f, 0.8f);

            layer.SetBlendMode(CodeDrawLayer.BlendMode.NONE);
            layer.DrawRect(230, 5, 300, 40, 0.2f, 0.4f, 1.0f, 0.5f + 0.5f * MathF.Sin(_tOverlay * 2f));
            layer.SetBlendMode(CodeDrawLayer.BlendMode.SOURCE_OVER_ALPHA);

            layer.Render();
        };

        _winLayerB.UpdateDelayMs = 33;
        _winLayerB.OnUpdate = ctx =>
        {
            _tOverlay += ctx.DeltaSeconds;

            var layer = ctx.Win.Layer;
            if (layer is null || layer.IsDisposed) return;

            layer.EnsureCanvas(800, 500);
            layer.Clear();
            layer.DrawRect(10, 5, 240, 40, 0.8f, 0.2f, 0.5f, 0.5f + 0.5f * MathF.Sin(_tOverlay * 2f));
            layer.DrawRect(400 + 100 * MathF.Sin(_tOverlay * 2f), 250 + 100 * MathF.Cos(_tOverlay * 2f), 20 + 10 * MathF.Cos(_tOverlay * 5f), 20 + 10 * MathF.Sin(_tOverlay * 5f), 0.8f, 0.2f, 0.5f, 1f);
            layer.Render();
        };

        _winCombined.OnUpdate = ctx =>
        {
            var layer = ctx.Win.Layer;
            if (layer is null || layer.IsDisposed) return;

            layer.EnsureCanvas(800, 500);
            layer.Clear(0.05f, 0.05f, 0.05f, 1f);

            if (_winLayerA is { IsDisposed: false, ShouldClose: false, Layer.IsDisposed: false })
            {
                layer.DrawLayer(_winLayerA.Layer);
            }

            if (_winLayerB is { IsDisposed: false, ShouldClose: false, Layer.IsDisposed: false })
            {
                layer.DrawLayer(_winLayerB.Layer);
            }

            layer.Render();
        };
    }

    public void WaitForClose()
    {
        _winLayerA.WaitForClose();
        _winLayerB.WaitForClose();
        _winCombined.WaitForClose();
    }

    public void Dispose()
    {
        _winLayerA.Dispose();
        _winLayerB.Dispose();
        _winCombined.Dispose();
    }
}
