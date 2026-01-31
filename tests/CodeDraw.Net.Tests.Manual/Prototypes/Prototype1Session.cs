namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed class Prototype1Session : IDisposable
{
    private readonly SharedGlfwHost _host;
    private readonly CodeDrawWindow _winA;
    private readonly CodeDrawWindow _winB;
    private readonly CodeDrawLayer _overlay;
    private readonly CancellationTokenSource _cts = new();

    private readonly Thread _tA;
    private readonly Thread _tB;

    public Prototype1Session(SharedGlfwHost host)
    {
        _host = host;

        _winA = new CodeDrawWindow(host, 800, 450, "A");
        _winB = new CodeDrawWindow(host, 800, 450, "B (mirrors A)");
        _winB.SetPresentedLayer(_winA.Layer);

        _overlay = new CodeDrawLayer(host, 800, 450);

        var inputThread = new Thread(() =>
        {
            while (!_winA.ShouldClose && !_winB.ShouldClose)
            {
                host.Input.Pump();
                Thread.Sleep(4);
            }
        })
        { IsBackground = true, Name = "InputPump" };

        inputThread.Start();

        host.Input.MouseMove += e => Console.WriteLine(e);
        host.Input.MouseButton += e => Console.WriteLine(e);
        host.Input.MouseButtonDown += e => Console.WriteLine(e);
        host.Input.MouseButtonUp += e => Console.WriteLine(e);
        host.Input.Key += e => Console.WriteLine(e);
        host.Input.KeyDown += e => Console.WriteLine(e);
        host.Input.KeyUp += e => Console.WriteLine(e);

        _tA = new Thread(() => RunA(_cts.Token)) { IsBackground = true, Name = "Update-A" };
        _tB = new Thread(() => RunOverlay(_cts.Token)) { IsBackground = true, Name = "Update-Overlay" };
        _tA.Start();
        _tB.Start();
    }

    private void RunA(CancellationToken ct)
    {
        float t = 0f;
        while (!ct.IsCancellationRequested && !_winA.ShouldClose)
        {
            t += 0.016f;

            var layer = _winA.Layer;
            if (layer is null || layer.IsDisposed) break;

            layer.EnsureCanvas(800, 450);
            layer.Clear(0.10f, 0.11f, 0.13f, 1f);

            layer.DrawRect(60 + 120 * MathF.Sin(t), 80, 220, 140, 0.2f, 1.0f, 0.6f, 1f);
            layer.DrawRect(90, 260, 140, 80, 1.0f, 0.3f, 0.2f, 0.9f);

            layer.SetBlendMode(CodeDrawLayer.BlendMode.ALPHA);
            layer.DrawLayer(_overlay);

            layer.Render();
            Thread.Sleep(16);
        }
    }

    private void RunOverlay(CancellationToken ct)
    {
        float t = 0f;
        while (!ct.IsCancellationRequested && !_winB.ShouldClose)
        {
            t += 0.033f;

            _overlay.EnsureCanvas(800, 450);
            _overlay.Clear();
            _overlay.SetBlendMode(CodeDrawLayer.BlendMode.ALPHA);
            _overlay.DrawRect(10, 10, 260, 40, 0.2f, 0.4f, 1.0f, 0.5f + 0.5f * MathF.Sin(t * 2f));
            _overlay.Render();

            Thread.Sleep(33);
        }
    }

    public void Dispose()
    {
        // 1) Stop threads
        _cts.Cancel();

        // 2) Stop windows (forces presenter loops to exit)
        _winA.Close();
        _winB.Close();

        // 3) Join worker threads
        _tA.Join();
        _tB.Join();

        // 4) Join presenter threads
        _winA.WaitForClose();
        _winB.WaitForClose();

        // 5) Dispose resources after threads ended
        _overlay.Dispose();
        _winA.Layer?.Dispose();

        _cts.Dispose();
    }
}
