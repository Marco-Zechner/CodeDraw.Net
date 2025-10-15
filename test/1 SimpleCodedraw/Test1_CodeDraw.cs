using MarcoZechner.ColorLib;
using MarcoZechner.Math;

namespace MarcoZechner.CodeDrawDotNet.Test1;

public class Test1_CodeDraw
{
    public static void Run()
    {

        var bsp1 = new CodeDraw("Beispiel 1")
        {
            Size = new Vector2<int>(300, 300),
            Resizable = false
        };

        bsp1.Clear(Color.WHITE);

        bsp1.Shapes.DrawColor = Color.GOLD;
        bsp1.Shapes.FillCircle(150, 150, 100);
        bsp1.Shapes.DrawColor = Color.SILVER;
        bsp1.Shapes.FillCircle(150, 150, 75);
        bsp1.Shapes.DrawColor = Color.Lerp(Color.SILVER, Color.BLACK, 0.2f);
        bsp1.Shapes.DrawText(150, 150, $"{1}");

        bsp1.Show();
    }
}