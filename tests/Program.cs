using MarcoZechner.CodeDraw.Net;
using MarcoZechner.ColorLib;

namespace MarcoZechner.CodeDraw.NetTest;

public class Program
{
    public static void Main(string[] args)
    {
        // CodeDrawOptions codeDrawOptions = new();
        // codeDrawOptions.WindowBorder = WindowBorder.Fixed;
        // codeDrawOptions.Size = new(300, 300);
        // codeDrawOptions.Title = "Beispiel 1";

        // var bsp1 = new CodeDrawWindow(codeDrawOptions);

        // bsp1.Clear(Color.WHITE);

        // bsp1.DrawColor = Color.GOLD;
        // bsp1.FillCircle(150, 150, 100);
        // bsp1.DrawColor = Color.SILVER;
        // bsp1.FillCircle(150, 150, 75);

        // bsp1.Show();

        // CodeDrawWindow.WaitTillAllWindowsClosed();

        var window = new GLFWWindow(OnLoad);

        window.Resize += (size) =>
        {
            // Console.WriteLine($"Window resized to {size.X}x{size.Y}");
        };

        window.Render += (dt) =>
        {
            // Console.WriteLine($"Render frame, dt={dt}");
            // Thread.Sleep(Random.Shared.Next(1, 10));
        };

        Console.ReadKey();

        Console.WriteLine("Press any key to exit...");

        while (GLFWWindow.WindowCount > 0 && !Console.KeyAvailable)
        {
            Thread.Sleep(100);
        }
    }

    private static void OnLoad()
    {
        Console.WriteLine("Window loaded - static");
    }
}