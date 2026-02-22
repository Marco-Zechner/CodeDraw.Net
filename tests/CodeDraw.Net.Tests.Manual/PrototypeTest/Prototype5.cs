using MarcoZechner.CodeDrawDotNet.Text;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public class Prototype5
{

    [ConstructorPrototype(5)]
    public Prototype5()
    {
        using var app = CodeDrawHost.Started();
        
        var win = new CodeDrawWindow(720, 420, 50, 50, "Prototype5 - Text Debug");

        win.ResizeMode = WindowResizeMode.Aspect;
        win.TransparentAlpha = false;

        var style = new TextStyle
        {
            Font = FontRef.FromFile(@"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf")
                .WithVariant(FontVariant.BoldItalic),
            SizePx = 48,

            Align = TextAlign.Center,
            VAlign = TextVAlign.Middle,

            Color = new ColorF(1, 1, 1, 1),

            // Start with “tight” (no extra) so you can see overlaps if metrics are wrong
            ExtraAbovePx = 0,
            ExtraBelowPx = 0,
            ExtraLineGapPx = 0,
            ExtraCellGapPx = 0,

            DebugMode = TextDebugMode.GlyphBoxes //TODO: the glyph boxes have... not all full boxes, it looks like some parts of the edge are missing.
        };

        win.OnUpdate += ctx =>
        {
            var layer = ctx.Win.Layer;
            layer.Clear(1, 1, 1, 1);

            // draw text with debug overlay
            layer.DrawText("Hello-\nWorld\nÄÖÜ gyp █", layer.Width / 2f, layer.Height / 2f, style);

            // draw measured bounds for sanity
            var m = layer.MeasureText("Hello\nWorld\nÄÖÜ gyp █", style);
            layer.DrawDebugRect(layer.Width / 2f - m.X / 2f, layer.Height / 2f - m.Y / 2f, m.X, m.Y, 0, 0, 1, 0.15f);

            // target anchor marker (should be center for Align/VAlign)
            layer.DrawDebugRect(layer.Width / 2f - 2, layer.Height / 2f - 2, 4, 4, 1, 1, 0, 1);
            
            layer.Render();
        };
        
        app.WaitForClose();
    }
}
