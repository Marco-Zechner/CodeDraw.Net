using MarcoZechner.CodeDraw.Net;
using MarcoZechner.ColorLib;

namespace MarcoZechner.CodeDraw.NetTest;

public class Program
{
    public static void Main(string[] args)
    {
        CodeDrawOptions codeDrawOptions = new();
        codeDrawOptions.WindowBorder = WindowBorder.Fixed;
        codeDrawOptions.Size = new(300, 300);
        codeDrawOptions.Title = "Beispiel 1";

        var bsp1 = new CodeDrawWindow(codeDrawOptions);

        bsp1.Clear(Color.WHITE);

        bsp1.DrawColor = Color.GOLD;
        bsp1.FillCircle(150, 150, 100);
        bsp1.DrawColor = Color.SILVER;
        bsp1.FillCircle(150, 150, 75);

        bsp1.Show();

        CodeDrawWindow.WaitTillAllWindowsClosed();
    }
}