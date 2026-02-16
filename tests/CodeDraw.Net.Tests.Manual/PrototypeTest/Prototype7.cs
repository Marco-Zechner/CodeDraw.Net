// File: tests/CodeDraw.Net.Tests.Manual/PrototypeTest/Prototype7.cs
using System.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(7)]
public sealed class Prototype7 : IDisposable
{
    private static SharedGlfwHost _host = null!;

    [StaticPrototype]
    public static void RunTest()
    {
        _host = SharedGlfwHost.Instance;
        _host.Start();

        using (new Prototype7())
        {
            _host.WaitUntilAllWindowsClosed();
        }

        _host.Stop();
        _host.Dispose();
    }

    private readonly List<CodeDrawWindow> _windows = [];

    public void Dispose()
    {
        foreach (var w in _windows) w.Dispose();
    }

    public Prototype7()
    {
        var win = new CodeDrawWindow(_host, 1100, 720, 50, 50, "Prototype7 - Text Render Showcase");
        _windows.Add(win);

        win.ResizeMode = WindowResizeMode.Aspect;
        win.TransparentAlpha = false;

        // --- State toggles ---
        var showDebug = false;
        var showBg = true;
        var snap = true;

        var t = 0f;
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
            Color = new Rgba(0.1f, 0.1f, 0.1f, 1f),
            Background = null,
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
            Color = new Rgba(0.05f, 0.05f, 0.05f, 0.95f),
            Background = null,
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
            Color = new Rgba(1f, 1f, 1f, 1f),
            Background = new Rgba(0.08f, 0.1f, 0.14f, 0.9f),
            DebugMode = TextDebugMode.None,
            DebugRects = DebugRectMode.Outline,
            DebugOutlinePx = 1,
            MonospaceSnapLineAlignToCells = true
        };

        var styleHud = new TextStyle
        {
            Font = baseFont.WithVariant(FontVariant.Regular),
            SizePx = 18,
            Align = TextAlign.Left,
            VAlign = TextVAlign.Top,
            Color = new Rgba(1f, 1f, 1f, 0.95f),
            Background = new Rgba(0f, 0f, 0f, 0.55f),
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
            "DDDDD\n" +
            "ÄÖÜ\n" +
            "gyp";

        win.OnUpdate += ctx =>
        {
            // --- Input ---
            if (ctx.Input.GetKeyDown(Keys.F1)) showDebug = !showDebug;
            if (ctx.Input.GetKeyDown(Keys.F2)) snap = !snap;
            if (ctx.Input.GetKeyDown(Keys.F3)) showBg = !showBg;

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

            // Optional backgrounds
            styleMonoBig.Background = showBg ? new Rgba(0.08f, 0.1f, 0.14f, 0.9f) : null;
            styleHud.Background = new Rgba(0f, 0f, 0f, 0.55f);

            // --- Simple "paper" margins ---
            var pad = 24f;

            // --- Title block ---
            layer.DrawText(demoTitle, pad, pad, styleTitle);

            // --- Body block ---
            var bodyY = pad + 60f;
            layer.DrawText(demoBody, pad, bodyY, styleBody);

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
            hud.AppendLine($"Mono-bg: {(showBg ? "ON" : "OFF")}");
            hud.AppendLine($"UPS: {ups:0.0}");

            var hudSize = layer.MeasureText(hud.ToString(), styleHud);
            
            layer.DrawText(hud.ToString(), pad, layer.Height - pad - hudSize.Y, styleHud);

            layer.Render();
        };
    }
}
