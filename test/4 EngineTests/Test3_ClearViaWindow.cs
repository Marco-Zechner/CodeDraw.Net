using MarcoZechner.CodeDrawDotNet.Api;
using MarcoZechner.ColorLib;

namespace MarcoZechner.Tests;

[Order(3)]
public class Test3_ClearViaWindow : ITestable
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
        win.Update += (w, dt) =>
        {
            Thread.Sleep(500);
            win.Clear(new Color(1f, 0.07f, 0.1f, 1f)); // acts like w.a2
            win.Show();
            Thread.Sleep(500);
            win.Clear(new Color(0.05f, 0.07f, 0.1f, 1f)); // acts like w.a4
            win.Show();
        };

        win.Open();

        win.WaitForClose();

        Console.WriteLine("Expected: window opens, clears to 2 different colors, (switches all 500ms), responsive.");
        Console.WriteLine("Press ENTER to exit…");
        Console.ReadLine();
    }
}
