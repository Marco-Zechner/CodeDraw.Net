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

        CodeDrawEvents.OnWindowLoaded += (w, gl, glfw, window) =>
        {
            Console.WriteLine($"Onload Called: {w.Title}");
        };

        win.Open(); // starts render thread
        win.EnqueueGL(gl => 
        {
            Console.WriteLine("EnqueueGL: setting clear color to red");
        });
        win.Clear(new Color(0f, 0f, 0f, 1f)); // clear to black
        win.Show();

        win.WaitForRender();

        Console.WriteLine("Expected: window opens, clears to black background, events work (onloadcalled) (move/resize).");
        Console.WriteLine("Press ENTER to exit…");

        win.Dispose();

        Console.ReadLine();
    }

}