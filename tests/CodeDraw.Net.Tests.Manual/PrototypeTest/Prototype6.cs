using System.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(6)]
public class Prototype6 : IDisposable
{
    private static SharedGlfwHost _host = null!;

    [StaticPrototype]
    public static void RunTest()
    {
        _host = SharedGlfwHost.Instance;
        _host.Start();

        using (new Prototype6())
        {
            _host.WaitUntilAllWindowsClosed();
        }

        _host.Stop();
        _host.Dispose();
    }

    public void Dispose()
    {
        foreach (var w in _windows) w.Dispose();
    }

    private readonly List<CodeDrawWindow> _windows = [];

    public Prototype6()
    {
        var window = new CodeDrawWindow(_host, 900, 700, 50, 50, "Prototype6 - Grid Test");
        _windows.Add(window);

        const int FONT_PX = 48;
        var padding = (X: 12, Y: 12);

        var style = new TextStyle
        {
            Font = FontRef.FromFile(@"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf")
                .WithVariant(FontVariant.Regular),
            SizePx = FONT_PX,

            Align = TextAlign.Left,
            VAlign = TextVAlign.Top,

            Color = new Rgba(1, 1, 1, 0.9f),

            ExtraAbovePx = 0,
            ExtraBelowPx = 0,
            ExtraLineGapPx = 0,
            ExtraCellGapPx = 0,

            DebugMode = TextDebugMode.Cells // only show cells for grid verification
        };

        var wall = new StringBuilder();
        float lastReset = -999;
        float resetAfter = 1.0f;

        window.OnUpdate += ctx =>
        {
            if (ctx.Input.GetKeyDown(Keys.A))
                ctx.Win.ToggleResizeMode(WindowResizeMode.Aspect);

            var layer = ctx.Win.Layer;
            layer.Clear(0, 0, 0, 1);

            // Find cell metrics by measuring 1x1 and 1x2 blocks (cheap + reliable for now)
            // (This avoids needing a new public API method right now.)
            var m1 = layer.MeasureText("█", style);
            var m2 = layer.MeasureText("█\n█", style);
            float cellW = m1.X;
            float lineH = m2.Y / 2f;

            int cols = (int)((layer.Width - padding.X * 2) / cellW);
            int rows = (int)((layer.Height - padding.Y * 2) / lineH);

            if (layer.LayerAliveForSeconds() - lastReset > resetAfter)
            {
                lastReset = layer.LayerAliveForSeconds();
                wall.Clear();

                for (int y = 0; y < rows; y++)
                {
                    wall.Append(RandomString(cols, "█"));
                    if (y != rows - 1) wall.Append('\n');
                }
            }
            
            // draw a testcell background
            layer.DrawRect(padding.X, padding.Y, cellW, lineH, 0, 0, 1, 0.15f);

            layer.DrawText(wall.ToString(), padding.X, padding.Y, style);

            
            // draw padding guides
            layer.DrawRect(0, 0, layer.Width, padding.Y, 1, 0, 0, 0.25f);
            layer.DrawRect(0, layer.Height - padding.Y, layer.Width, padding.Y, 1, 0, 0, 0.25f);
            layer.DrawRect(0, 0, padding.X, layer.Height, 1, 0, 0, 0.25f);
            layer.DrawRect(layer.Width - padding.X, 0, padding.X, layer.Height, 1, 0, 0, 0.25f);

            layer.Render();
        };
    }

    private static readonly Random _random = new();

    public static string RandomString(int length, string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
    {
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}
