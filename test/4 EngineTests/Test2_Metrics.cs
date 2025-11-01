using MarcoZechner.ColorDotNet;
using MarcoZechner.CodeDrawDotNet.Api;

namespace MarcoZechner.Tests;

[Order(2)]
public class Test2_Metrics : ITestable
{
    public void RunTest()
    {
        var win = new CodeDrawWindow("Test2_Metrics")
        {
            Size = new(640, 360),
            // TargetFPS = 1000,
            VSync = true, //TODO without vsync, fps is too low?
            Resizable = true,
            ClearColor = new Color(0.08f, 0.1f, 0.13f, 1.0f),
        };

        double acc = 0;

        win.Update += (w, dt) =>
        {
            acc += dt;

            // Simple frame: just clear with current clear color
            // w.Clear(w.ClearColor);
            w.Show();

            // Heartbeat ~1s
            if (acc >= 1.0)
            {
                acc = 0;

                var winUp = w.Uptime.TotalSeconds;

                Console.WriteLine(
                    $"EngineUp={CodeDraw.EngineUptime.TotalSeconds,5:0.00}s  WinUp={winUp,5:0.00}s  FPS={w.FPS,5:0.00}  UPS ={w.UPS,5:0.00}  " +
                    $"\nEventUPS= {CodeDraw.EventLoopUPS.ToShortString()}  " +
                    $"\nLayerMetrics= {CodeDraw.LayerWorkerMetrics.ToShortString()}");
            }
        };

        win.Open();

        Console.WriteLine("Expected: window opens, background drawn; console logs EngineUp/WinUp, FPS, UpdateUPS, EventUPS ~each second.");
        Console.WriteLine("Press ENTER to exit…");
        Console.ReadLine();

        win.Dispose();
    }
}
