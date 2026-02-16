using Legacy1.MarcoZechner.CodeDrawDotNet.CodeDrawSettings.FontSettings;
using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.Tests.Manual.Tests;

public class Test1CodeDraw
{
    public static long FrameCount => _bsp1.FrameCount;
    private static long _frameOffset = 0;
    private static CodeDraw _bsp1 = null!;
    public static void OffsetNow()
    {
        _frameOffset = _bsp1.FrameCount;
    }

    public static void Run()
    {

        _bsp1 = new("Beispiel 1.1")
        {
            Size = new Vector2<int>(800, 300),
        };
        _bsp1.Resizable = true;

        var tf = _bsp1.Shapes.TextFormat;
        tf.HorizontalAlignment = HorizontalAlignment.CENTER;
        tf.VerticalAlignment = VerticalAlignment.MIDDLE;
        tf.FontSize = 80;
        _bsp1.Shapes.TextFormat = tf;

        Color color1 = Color.Gold;
        Color color2 = Color.Silver;

        Task.Run(() =>
        {
            while (_bsp1.IsRunning)
            {
                if (_bsp1.Input.GetKeyDown(Keys.Escape))
                    _bsp1.Close();

                // Logger.Log("\t\tChecking for space key...");
                if (_bsp1.Input.GetKeyDown(Keys.Space))
                {
                    // Console.WriteLine(" pressed!");
                    (color1, color2) = (color2, color1);
                }
                else
                {
                    // Console.WriteLine(" --- ");
                }

                _bsp1.Clear(Color.White);

                _bsp1.Shapes.DrawColor = color1;
                _bsp1.Shapes.FillCircle(150, 150, 100);
                _bsp1.Shapes.DrawColor = color2;
                _bsp1.Shapes.FillCircle(150, 150, 75);
                _bsp1.Shapes.DrawColor = Color.Lerp(color2, Color.Black, 0.2f);
                _bsp1.Shapes.DrawText(150, 150, $"{(color2 == Color.Silver ? 2 : 1)}");

                float movingX = (_bsp1.FrameCount - _frameOffset) % 300;
                _bsp1.Shapes.DrawColor = Color.Red;
                _bsp1.Shapes.FillRectangle(movingX, 280, 20, 20);

                _bsp1.Show();
            }
        });
    }
}