namespace MarcoZechner.CodeDrawDotNet.EngineTests;

class Test1_OpenWindow : ITestable
{
    public void RunTest()
    {
        var win = new CodeDrawWindow("MiniTest: Open Window")
        {
            Size = new(640, 360),
            Resizable = true,
            TargetFPS = 60
        };

        // Optional: observe load
        CodeDrawEvents.OnWindowLoaded += (w, gfx) =>
        {
            Console.WriteLine($"Loaded: {w.Title}");
        };

        win.Open(); // starts render thread

        Console.WriteLine("Press ENTER to exit…");
        Console.ReadLine();
        // (Close the window via OS chrome to exit the render loop.)
    }

}