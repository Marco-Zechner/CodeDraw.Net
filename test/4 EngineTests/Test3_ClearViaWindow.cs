using System;
using MarcoZechner.CodeDrawDotNet;
using MarcoZechner.ColorLib;

namespace MarcoZechner.CodeDrawDotNet.EngineTests;

[Order(3)]
class Test3_ClearViaWindow : ITestable
{
    public void RunTest()
    {
        var win = new CodeDrawWindow("Test3_ClearViaWindow")
        {
            Size = new(640, 360),
            Resizable = true,
            TargetFPS = 60,
            ClearColor = new Color(0.10f, 0.12f, 0.16f, 1f)
        };

        // No gfx usage. Use the high-level API only.
        win.BeforeRender += (w, gfx, dt) =>
        {
            // Use the configured ClearColor to keep behavior consistent
            w.Clear(w.ClearColor);
            w.Show();
        };

        win.Open();

        Console.WriteLine("Expected: window opens, clears to the configured color each frame, responsive.");
        Console.WriteLine("Press ENTER to exit…");
        Console.ReadLine();
    }
}
