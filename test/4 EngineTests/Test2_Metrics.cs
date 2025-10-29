using MarcoZechner.ColorLib;

namespace MarcoZechner.CodeDrawDotNet.EngineTests;

[Order(2)]
class Test2_Metrics : ITestable
{
    public void RunTest()
    {
        var win = new CodeDrawWindow("Test2_Metrics")
        {
            Size = new(640, 360),
            TargetFPS = 60,
            Resizable = true,
            ClearColor = new Color(0.08f, 0.1f, 0.13f, 0.5f),
            UpdateIntervalMs = 2000
        };

        double acc = 0;

        long lastFrame = 0;
        HashSet<long> missed = [];

        win.Update += (w, dt) =>
        {
            acc += dt;

            if (w.Frames % 120 == 0)
            {
                w.EnqueueGL(gfx =>
                {
                    // Thread.Sleep(20);
                });
            }

            w.Show();

            if (lastFrame + 1 < w.Frames)
            {
                for (long i = lastFrame + 1; i < w.Frames; i++)
                {
                    missed.Add(i);
                }
            }
            lastFrame = w.Frames;

            // log a heartbeat every ~1s using dt accumulation
            if (acc >= 1.0)
            {
                Console.WriteLine($"dt≈{dt:0.000}s  Frames={w.Frames}  Uptime={w.Uptime.TotalSeconds:0.0}s  EngineUp={CodeDraw.EngineUptime.TotalSeconds:0.0}s");
                if (missed.Count > 0)
                {
                    Console.WriteLine("Missed Frames since last time: " + string.Join(", ", missed)); //skips a lot of frames...
                    missed.Clear();
                }
                acc = 0;
            }
        };

        win.Open();

        Console.WriteLine("Expected: window shows dark semi transparent background; console logs dt, Frames, Uptime each ~1s.");
        Console.WriteLine("Press ENTER to exit…");
        Console.ReadLine();
    }
}
