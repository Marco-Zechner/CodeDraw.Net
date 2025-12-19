using MarcoZechner.ColorDotNet;
using MarcoZechner.CodeDrawDotNet.Api;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual;

[Order(2)]
public class Test2Metrics : ITestable
{
    public void RunTest()
    {
        var win = new CodeDrawWindow("Test2_Metrics")
        {
            Size = new(640, 360),
            // TargetFPS = 0, // caping doesn't work correctly yet. (is always slower than target)
            UpdateIntervalMs = 1,
            MaxInflightFrames = 50,
            VSync = true,
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

                Console.WriteLine($@"
Engine Uptime : {CodeDraw.EngineUptime.TotalSeconds,-8:0.00}s
Event UPS     : {CodeDraw.EventLoopUps.ToShortString(),-20}
Layer Metrics : {CodeDraw.LayerWorkerMetrics.ToShortString(),-20}
------------------------------------------------------------
Window Uptime : {winUp,-8:0.00}s   FPS: {w.Fps,6:0.00}   UPS: {w.Ups,6:0.00}
Backlog       : {w.BacklogFrames,-9}   Queue: {w.QueuedFrames,-4}   Inflight: {w.InflightFrames}
");
            }
        };

        win.Open();

        Console.WriteLine("Expected: window opens, background drawn; console logs EngineUp/WinUp, FPS, UpdateUPS, EventUPS ~each second.");
        Console.WriteLine("Press ENTER to exit…");
        Console.ReadLine();
    }
}
