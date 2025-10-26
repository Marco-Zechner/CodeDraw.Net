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
            ClearColor = new Color(0.08f, 0.1f, 0.13f, 0.5f)
        };

        double acc = 0;

        win.BeforeRender += (w, gfx, dt) =>
        {
            acc += dt;

            // clear so we see a stable background
            gfx.ClearColor(w.ClearColor.R, w.ClearColor.G, w.ClearColor.B, w.ClearColor.A);
            gfx.Clear();

            // log a heartbeat every ~1s using dt accumulation
            if (acc >= 1.0)
            {
                Console.WriteLine($"dt≈{dt:0.000}s  Frames={w.Frames}  Uptime={w.Uptime.TotalSeconds:0.0}s  EngineUp={CodeDraw.EngineUptime.TotalSeconds:0.0}s");
                acc = 0;
            }
        };

        win.Open();

        Console.WriteLine("Expected: window shows dark semi transparent background; console logs dt, Frames, Uptime each ~1s.");
        Console.WriteLine("Press ENTER to exit…");
        Console.ReadLine();
    }
}
