using MarcoZechner.ColorLib;

namespace MarcoZechner.CodeDrawDotNet.EngineTests;

[Order(1)]
class Test1_OpenWindow : ITestable
{
    public void RunTest()
    {
        var win = new CodeDrawWindow("Test1_OpenWindow")
        {
            Size = new(640, 360),
            Resizable = true,
            TargetFPS = 60
        };

        win.BeforeRender += (w, gfx, dt) =>
        {
            // Clear to black each frame
            gfx.ClearColor(0, 0, 0, 1);
            gfx.Clear();
        };

        // Optional: observe load
        CodeDrawEvents.OnWindowLoaded += (w, gfx) =>
        {
            Console.WriteLine($"Loaded: {w.Title}");
        };

        win.Open(); // starts render thread

        Console.WriteLine("Expected: window opens, clears to black background, events work (move/resize).");
        Console.WriteLine("Press ENTER to exit…");
        Console.ReadLine();
    }

}