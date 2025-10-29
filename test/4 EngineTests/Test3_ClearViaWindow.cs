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
        };

        // Clear window via high-level API each frame. Check if lower-level & highlevel mix preserves order
        // win.BeforeRender += (w, dt) =>
        // {
        //     win.Enqueue(g =>
        //     {
        //         Console.WriteLine("gfx.a1, press enter for red.");
        //         Console.ReadLine();
        //     });
        //     win.Clear(new Color(1f, 0.07f, 0.1f, 1f)); // acts like w.a2
        //     win.Show();
        //     win.Enqueue(g =>
        //     {
        //         Console.WriteLine("gfx.a3, press enter for blue gray");
        //         Console.ReadLine();
        //     });
        //     win.Clear(new Color(0.05f, 0.07f, 0.1f, 1f)); // acts like w.a4
        //     win.Show();
        //     win.WaitForRender();
        //     win.WaitForRender();
        // };

        win.Open();

        Console.WriteLine("Expected: window opens, clears to the configured color each frame, responsive.");
        Console.WriteLine("Press ENTER to exit…");
        Console.ReadLine();
    }
}
