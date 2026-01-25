using MarcoZechner.CodeDrawDotNet.Api;
using MarcoZechner.ColorDotNet;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.EngineTests;

[Order(4)]
public class Test4DrawRect2D : ITestable
{
    public void RunTest()
    {
        var win = new CodeDrawWindow("Test4_DrawRect2D")
        {
            Size = new(900, 500),
            Resizable = true,
            VSync = true,
            UpdateIntervalMs = 16,
            ClearColor = new Color(0.08f, 0.1f, 0.13f, 1.0f),
        };

        float t = 0;

        win.Update += (w, dt) =>
        {
            t += (float)dt;

            // Clear canvas each frame so motion is obvious
            w.Clear(w.ClearColor);

            // Static reference rect (top-left)
            w.FillRect(20, 20, 200, 80, new Color(0.2f, 0.8f, 0.3f, 1f));

            // Moving rect
            float x = 260 + (float)Math.Sin(t * 1.5f) * 200;
            float y = 220 + (float)Math.Cos(t * 1.2f) * 120;

            w.FillRect(x, y, 160, 100, new Color(0.95f, 0.25f, 0.2f, 0.9f));

            w.Show();
        };

        win.Key += (k, sc, a, m) =>
        {
            if (k == Silk.NET.GLFW.Keys.Escape && a == Silk.NET.GLFW.InputAction.Press)
                win.Close();
        };

        win.Open();

        Console.WriteLine("Expected: dark background, one static green rect, one moving red rect. ESC closes.");
        win.WaitForClose();
        Console.WriteLine("Closed. Press ENTER to exit…");
        Console.ReadLine();
    }
}