using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual;

public class Test2_CodeDraw
{
    private static bool _next = true;

    private static CodeDraw _cd = null!;

    private static readonly Color _bg = Color.WHITE;
    private static readonly Color _coin1 = Color.GOLD;
    private static readonly Color _coin2 = Color.SILVER;

    public static long FrameCount => _cd.FrameCount;
    private static long _frameOffset = 0;
    public static void OffsetNow()
    {
        _frameOffset = _cd.FrameCount;
    }

    public static void Run()
    {
        _cd = new("Beispiel 1.2", true);
        // _cd.Title = "Beispiel 1.2 - Interner Loop"; // crash. i think because its not called on the render thread.
        // _cd.Size = new Vector2<int>(300, 300); //crash
        // _cd.Resizable = true; // crash

        _cd.AutoRender = true;
        _cd.MonitorRendering = false;

        _cd.OnLoad += Load;
        _cd.Input.OnKeyDown += ProcessKey;
        _cd.Input.OnKey += ProcessKey;
        _cd.OnRender += Render;
        _cd.Run();
    }


    private static void Load()
    {
        //TODO: yes, can only be called on the render thread. fix this.
        _cd.Title = "Beispiel 1.2 - Interner Loop";
        _cd.Size = new Vector2<int>(800, 300);
        _cd.Resizable = true;


        var tf = _cd.Shapes.TextFormat;
        tf.HorizontalAlignment = HorizontalAlignment.Center;
        tf.VerticalAlignment = VerticalAlignment.Middle;
        tf.FontSize = 80;
        _cd.Shapes.TextFormat = tf;
    }

    private static float _rnd = 0f;

    private static void Render(double dt, SKCanvas canvas, GL gl)
    {
        _cd.Clear(_bg);
        if (_next)
        {
            _next = false;
            Random random = new();
            _rnd = random.NextSingle();
        }

        if (_rnd > 0.5f)
        {
            _cd.Shapes.DrawColor = _coin1;
            _cd.Shapes.FillCircle(150, 150, 100);
            _cd.Shapes.DrawColor = _coin2;
            _cd.Shapes.FillCircle(150, 150, 75);
            _cd.Shapes.DrawColor = Color.Lerp(_coin2, Color.BLACK, 0.2f);
            _cd.Shapes.DrawText(150, 150, $"{1}");
        }
        else
        {
            _cd.Shapes.DrawColor = _coin2;
            _cd.Shapes.FillCircle(150, 150, 100);
            _cd.Shapes.DrawColor = _coin1;
            _cd.Shapes.FillCircle(150, 150, 75);
            _cd.Shapes.DrawColor = Color.Lerp(_coin1, Color.BLACK, 0.2f);
            _cd.Shapes.DrawText(150, 150, $"{2}");
        }

        float movingX = (_cd.FrameCount - _frameOffset) % 300;
        _cd.Shapes.DrawColor = Color.RED;
        _cd.Shapes.FillRectangle(movingX, 280, 20, 20);

        _cd.Show();
    }

    private static void ProcessKey(Keys key)
    {
        if (key == Keys.Escape)
        {
            // _cd.Close(); // crash
        }

        if (key == Keys.Space)
        {
            _next = true;
        }
    }
}