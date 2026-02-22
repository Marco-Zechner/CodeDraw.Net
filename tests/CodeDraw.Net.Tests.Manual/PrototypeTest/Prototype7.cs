using System.Text;
using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Text;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet.RGB;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public sealed class Prototype7
{
    [ConstructorPrototype(7)]
    public Prototype7()
    {
        using var app = CodeDrawHost.Started();
        
        var win = new CodeDrawWindow(1100, 720, 50, 50, "Prototype7 - Text Render Showcase");

        win.ResizeMode = WindowResizeMode.Aspect;
        win.TransparentAlpha = false;

        // --- State toggles ---
        var showDebug = false;
        var snap = true;

        var ups = 0f;
        var updates = 0;
        var upsAccum = 0f;

        // --- Styles ---
        var baseFont = FontRef.FromFile(
            @"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf"
        );

        var styleTitle = new TextStyle
        {
            Font = baseFont.WithVariant(FontVariant.Bold),
            SizePx = 44,
            Align = TextAlign.Left,
            VAlign = TextVAlign.Top,
            Color = new ColorF(0.1f, 0.1f, 0.1f, 1f),
            DebugMode = TextDebugMode.None,
            DebugRects = DebugRectMode.Outline,
            DebugOutlinePx = 1,
            MonospaceSnapLineAlignToCells = true
        };

        var styleBody = new TextStyle
        {
            Font = baseFont.WithVariant(FontVariant.Regular),
            SizePx = 26,
            Align = TextAlign.Left,
            VAlign = TextVAlign.Top,
            Color = new ColorF(0.05f, 0.05f, 0.05f, 0.95f),
            DebugMode = TextDebugMode.None,
            DebugRects = DebugRectMode.Outline,
            DebugOutlinePx = 1,
            MonospaceSnapLineAlignToCells = true
        };

        var styleMonoBig = new TextStyle
        {
            Font = baseFont.WithVariant(FontVariant.Regular),
            SizePx = 48,
            Align = TextAlign.Center,
            VAlign = TextVAlign.Top,
            Color = new ColorF(1f, 1f, 1f, 1f),
            Background = new ColorF(0.08f, 0.1f, 0.14f, 0.9f),
            DebugMode = TextDebugMode.None,
            DebugRects = DebugRectMode.Outline,
            DebugOutlinePx = 1,
            MonospaceSnapLineAlignToCells = true,
            BackgroundMode = TextBackgroundMode.PerLine,
            BackgroundIncludeSpaces = true,
            BackgroundPaddingPx = 4f,
            BackgroundBlendMode = BlendMode.NONE
        };

        var styleHud = new TextStyle
        {
            Font = baseFont.WithVariant(FontVariant.Regular),
            SizePx = 18,
            Align = TextAlign.Left,
            VAlign = TextVAlign.Top,
            Color = new ColorF(1f, 1f, 1f, 0.95f),
            Background = new ColorF(0f, 0f, 0f, 0.55f),
            DebugMode = TextDebugMode.None,
            DebugRects = DebugRectMode.Outline,
            DebugOutlinePx = 1,
            MonospaceSnapLineAlignToCells = true
        };

        // Content
        var demoTitle = "Text Rendering Showcase";
        var demoBody =
            "This is the \"normal use\" test.\n" +
            "No microscope overlays unless you toggle them.\n\n" +
            "German: ÄÖÜ äöü ß\n" +
            "Descenders: g y p q j\n" +
            "Block: █  ▓  ▒  ░\n" +
            "Punctuation: []{}()<> /\\ | _ -=+ *&^%$#@!\n";

        // The line-centering test you mentioned (4 vs 5 chars)
        var monoSnapTest =
            "AAAA\n" +
            "BBBBB\n" +
            "CCCC\n" +
            "DDD D\n" +
            "ÄÖÜ\n" +
            "gyp";

        win.OnUpdate += ctx =>
        {
            // --- Input ---
            if (ctx.Input.GetKeyDown(Keys.F1)) showDebug = !showDebug;
            if (ctx.Input.GetKeyDown(Keys.F2)) snap = !snap;
            if (ctx.Input.GetKeyDown(Keys.F3))
            {
                styleMonoBig.BackgroundMode = styleMonoBig.BackgroundMode switch
                {
                    TextBackgroundMode.None => TextBackgroundMode.PerCell,
                    TextBackgroundMode.PerCell => TextBackgroundMode.PerLine,
                    TextBackgroundMode.PerLine => TextBackgroundMode.PerGlyphBox,
                    _ => TextBackgroundMode.None,
                };
            }

            // --- Timing / FPS ---
            var dt = ctx.DeltaSeconds;

            updates++;
            upsAccum += dt;
            if (upsAccum >= 0.25f)
            {
                ups = updates / upsAccum;
                updates = 0;
                upsAccum = 0f;
            }

            var layer = ctx.Win.Layer;
            layer.Clear(0.7f, 0.7f, 0.75f, 1f);

            // --- Toggle style knobs (per-frame) ---
            var dbg = showDebug ? TextDebugMode.All : TextDebugMode.None;

            styleTitle.DebugMode = dbg;
            styleBody.DebugMode = dbg;
            styleMonoBig.DebugMode = dbg;
            styleHud.DebugMode = dbg;

            styleMonoBig.MonospaceSnapLineAlignToCells = snap;
            
            // --- Simple "paper" margins ---
            const float PAD = 24f;

            // --- Title block ---
            layer.DrawText(demoTitle, PAD, PAD, styleTitle);

            // --- Body block ---
            var bodyY = PAD + 60f;
            layer.DrawText(demoBody, PAD, bodyY, styleBody);

            // --- Monospace snap/center test block ---
            // Center anchor marker
            var cx = layer.Width*3/4f;
            var cy = layer.Height/3f;
            layer.DrawRect(cx - 2, cy - 2, 4, 4, 1f, 0.8f, 0.2f, 1f);

            styleMonoBig.Align = TextAlign.Center;
            styleMonoBig.VAlign = TextVAlign.Top;

            // Draw the test text centered relative to the anchor
            layer.DrawText(monoSnapTest, cx, cy, styleMonoBig);

            // Measured bounds overlay (helps spot alignment bugs in normal mode)
            var m = layer.MeasureText(monoSnapTest, styleMonoBig);

            var padBox = 18f;
            var x0 = cx - m.X * 0.5f - padBox;
            var y0 = cy - padBox;
            var w0 = m.X + padBox * 2f;
            var h0 = m.Y + padBox * 2f;

            layer.DrawRect(x0, y0, w0, h0, 0.2f, 0.2f, 0.25f, 0.15f);                     // gray panel
            layer.DrawRect(cx - m.X * 0.5f, cy, m.X, m.Y, 0.0f, 0.45f, 1.0f, 0.12f); // blue bounds

            // --- HUD / controls ---
            var hud = new StringBuilder();
            hud.AppendLine("F1  toggle debug overlays");
            hud.AppendLine("F2  toggle monospace snap-centering");
            hud.AppendLine("F3  toggle text backgrounds");
            hud.AppendLine();
            hud.AppendLine($"Snap-centering: {(snap ? "ON" : "OFF")}");
            hud.AppendLine($"Debug overlays: {(showDebug ? "ON" : "OFF")}");
            hud.AppendLine($"Mono-bg: {styleMonoBig.BackgroundMode.ToString()}");
            hud.AppendLine($"UPS: {ups:0.0}");

            var hudSize = layer.MeasureText(hud.ToString(), styleHud);
            
            layer.DrawText(hud.ToString(), PAD, layer.Height - PAD - hudSize.Y, styleHud);

            layer.Render();
        };
        
        app.WaitForClose();
    }
}
