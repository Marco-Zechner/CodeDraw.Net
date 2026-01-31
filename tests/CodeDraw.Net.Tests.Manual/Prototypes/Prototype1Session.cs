namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed class Prototype1Session : IDisposable
{
    private readonly CodeDrawWindow _winA;
    private readonly CodeDrawWindow _winB;
    private readonly CodeDrawLayer _overlay;

    private float _tA;
    private float _tOverlay;

    public Prototype1Session(SharedGlfwHost host)
    {
        _winA = new CodeDrawWindow(host, 800, 450, "A");
        _winB = new CodeDrawWindow(host, 800, 450, "B (mirrors A)");
        _winB.SetPresentedLayer(_winA.Layer);

        _overlay = new CodeDrawLayer(host, 800, 450);

        _winA.OnStart = w => Console.WriteLine($"A started (id={w.WindowId})");
        _winB.OnStart = w => Console.WriteLine($"B started (id={w.WindowId})");
        _winA.OnClose = w => Console.WriteLine($"A closed (id={w.WindowId})");
        _winB.OnClose = w => Console.WriteLine($"B closed (id={w.WindowId})");

        _winA.OnUpdate = ctx =>
        {
            _tA += ctx.DeltaSeconds;

            var layer = ctx.Win.Layer;
            if (layer is null || layer.IsDisposed) return;

            layer.EnsureCanvas(800, 450);
            layer.Clear(0.10f, 0.11f, 0.13f, 1f);

            layer.DrawRect(60 + 120 * MathF.Sin(_tA), 80, 220, 140, 0.2f, 1.0f, 0.6f, 1f);
            layer.DrawRect(90, 260, 140, 80, 1.0f, 0.3f, 0.2f, 0.9f);

            layer.SetBlendMode(CodeDrawLayer.BlendMode.ALPHA);
            layer.DrawLayer(_overlay);

            layer.Render();
        };

        _winB.UpdateDelayMs = 33;
        _winB.OnUpdate = ctx =>
        {
            _tOverlay += ctx.DeltaSeconds;

            _overlay.EnsureCanvas(800, 450);
            _overlay.Clear();
            _overlay.SetBlendMode(CodeDrawLayer.BlendMode.ALPHA);
            _overlay.DrawRect(10, 10, 260, 40, 0.2f, 0.4f, 1.0f, 0.5f + 0.5f * MathF.Sin(_tOverlay * 2f));
            _overlay.Render();
        };

        _winA.WaitForClose();
        _winB.WaitForClose();
    }

    public void Dispose()
    {
        _winA.Dispose();
        _winB.Dispose();
        _overlay.Dispose();
    }
}
