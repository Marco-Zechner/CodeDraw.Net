using MarcoZechner.ColorLib;
using MarcoZechner.Math;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet.Test2;

public class Test2_CodeDraw
{
    private static bool _next = true;
    
    private static CodeDraw _cd = null!;

    private static readonly Color _bg = Color.WHITE;
    private static readonly Color _coin1 = Color.GOLD;
    private static readonly Color _coin2 = Color.SILVER;

    public static void Run()
    {
        _cd = new("Beispiel 1", true);
        // _cd.Title = "Beispiel 2 - Interner Loop"; // crash. i think because its not called on the render thread.
        _cd.Size = new Vector2<int>(300, 300); //crash
        // _cd.Resizable = true; // crash

        _cd.AutoRender = true;
        _cd.MonitorRendering = false;

        _cd.OnLoad += Load;
        _cd.OnKeyDown += ProcessKey;
        _cd.OnKey += ProcessKey;
        _cd.OnRender += Render;
        _cd.Run();
    }


    private static void Load()
    {
        //TODO: yes, can only be called on the render thread. fix this.
        _cd.Title = "Beispiel 2 - Interner Loop";
        _cd.Size = new Vector2<int>(300, 300);
        _cd.Resizable = true;


        var tf = _cd.Shapes.TextFormat;
        tf.HorizontalAlignment = HorizontalAlignment.Center;
        tf.VerticalAlignment = VerticalAlignment.Middle;
        tf.FontSize = 80;
        _cd.Shapes.TextFormat = tf;
    }

    private static void Render(double dt, SKCanvas canvas, GL gl)
    {
        if (!_next)
            return;

        _next = false;

        _cd.Clear(_bg);
        Random random = new();
        float rnd = random.NextSingle();
        if (rnd > 0.5f)
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