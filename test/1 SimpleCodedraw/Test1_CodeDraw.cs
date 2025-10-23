using MarcoZechner.ColorLib;
using MarcoZechner.Math;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Test1;

public class Test1_CodeDraw
{
    public static void Run()
    {

        var bsp1 = new CodeDraw("Beispiel 1.1")
        {
            Size = new Vector2<int>(300, 300),
            Resizable = false
        };

        Color color1 = Color.GOLD;
        Color color2 = Color.SILVER;

        while (bsp1.IsRunning)
        {   
            if (bsp1.Input.GetKeyDown(Keys.Escape))
                bsp1.Close();

            Logger.Log("\t\tChecking for space key...");
            if (bsp1.Input.GetKeyDown(Keys.Space))
            {
                Console.WriteLine(" pressed!");
                (color1, color2) = (color2, color1);
            } else
                Console.WriteLine(" --- ");

            bsp1.Clear(Color.WHITE);

            bsp1.Shapes.DrawColor = color1;
            bsp1.Shapes.FillCircle(150, 150, 100);
            bsp1.Shapes.DrawColor = color2;
            bsp1.Shapes.FillCircle(150, 150, 75);
            bsp1.Shapes.DrawColor = Color.Lerp(color2, Color.BLACK, 0.2f);
            bsp1.Shapes.DrawText(150, 150, $"{(color2 == Color.SILVER ? 2 : 1)}");

            bsp1.Show();
        }
        
    }
}