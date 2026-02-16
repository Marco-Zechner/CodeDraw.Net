using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(5)]
public class Prototype5 : IDisposable
{
    [StaticPrototype]
    public static void RunTest()
    {
        var host = SharedGlfwHost.Instance;
        host.Start();

        using (new Prototype5(host))
        {
            host.WaitUntilAllWindowsClosed();
        }

        host.Stop();
    }

    public void Dispose()
    {
        foreach (var w in _windows) w.Dispose();
    }

    private readonly List<CodeDrawWindow> _windows = [];

    public Prototype5(SharedGlfwHost host)
    {
        var win = new CodeDrawWindow(host, 720, 420, 50, 50, "Prototype5 - Text Debug");
        _windows.Add(win);

        win.ResizeMode = WindowResizeMode.Aspect;
        win.TransparentAlpha = false;

        var style = new TextStyle
        {
            Font = FontRef.FromFile(@"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf")
                .WithVariant(FontVariant.BoldItalic),
            SizePx = 48,

            Align = TextAlign.Center,
            VAlign = TextVAlign.Middle,

            Color = new Rgba(1, 1, 1, 1),

            // Start with “tight” (no extra) so you can see overlaps if metrics are wrong
            ExtraAbovePx = 0,
            ExtraBelowPx = 0,
            ExtraLineGapPx = 0,
            ExtraCellGapPx = 0,

            DebugMode = TextDebugMode.All
        };

        win.OnUpdate += ctx =>
        {
            var layer = ctx.Win.Layer;
            layer.Clear(1, 1, 1, 1);

            // draw text with debug overlay
            layer.DrawText("Hello-\nWorld\nÄÖÜ gyp █", layer.Width / 2f, layer.Height / 2f, style);

            // draw measured bounds for sanity
            var m = layer.MeasureText("Hello\nWorld\nÄÖÜ gyp █", style);
            layer.DrawRect(layer.Width / 2f - m.X / 2f, layer.Height / 2f - m.Y / 2f, m.X, m.Y, 0, 0, 1, 0.15f);

            // target anchor marker (should be center for Align/VAlign)
            layer.DrawRect(layer.Width / 2f - 2, layer.Height / 2f - 2, 4, 4, 1, 1, 0, 1);
            
            layer.Render();
        };
    }
}
