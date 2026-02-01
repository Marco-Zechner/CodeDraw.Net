using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(2)]
public sealed class Prototype2 : IDisposable
{
    [StaticPrototype]
    public static void RunTest()
    {
        var host = SharedGlfwHost.Instance;
        host.Start();

        using (var session = new Prototype2(host))
        {
            session.WaitForClose();
        }

        host.Stop();
    }

    private readonly CodeDrawWindow _winSrc;
    private readonly CodeDrawWindow _winDst;
    private readonly CodeDrawWindow _winFull;

    private readonly CodeDrawLayer.CodeDrawShader _desatShader;

    private float _t;

    public Prototype2(SharedGlfwHost host)
    {
        _winSrc = new CodeDrawWindow(host, 800, 500, 50, 120, "2B: Source (Pattern Atlas)");
        _winDst = new CodeDrawWindow(host, 800, 500, 850, 120, "2B: Dest (Crop/Place Tests)");
        _winFull = new CodeDrawWindow(host, 800, 500, 1650, 120, "2B: Full (Copy Src fully, mostly desaturated)");

        _desatShader = new CodeDrawLayer.CodeDrawShader(host, DesatShaderVs, DesatShaderFs);

        _winSrc.OnStart = w => Console.WriteLine($"2B Src started (id={w.WindowId})");
        _winDst.OnStart = w => Console.WriteLine($"2B Dst started (id={w.WindowId})");
        _winFull.OnStart = w => Console.WriteLine($"2B Full started (id={w.WindowId})");
        _winSrc.OnClose = w => Console.WriteLine($"2B Src closed (id={w.WindowId})");
        _winDst.OnClose = w => Console.WriteLine($"2B Dst closed (id={w.WindowId})");
        _winFull.OnClose = w => Console.WriteLine($"2B Full closed (id={w.WindowId})");

        host.Input.OnKeyDown += ((win, key, mods) =>
        {
            switch (key)
            {
                case Keys.Escape:
                    win.Close();
                    break;
                case Keys.F11:
                    win.MaximizeBorderless = !win.MaximizeBorderless;
                    break;
            }
        });

        _winSrc.OnUpdate = ctx =>
        {
            _t += ctx.DeltaSeconds;

            var layer = ctx.Win.Layer;
            if (layer is null || layer.IsDisposed) return;

            const int W = 800;
            const int H = 500;

            layer.EnsureCanvas(W, H);
            layer.Clear(0.02f, 0.02f, 0.02f, 1f);

            // --- 1) Quadrants (unique colors) ---
            layer.SetBlendMode(CodeDrawLayer.BlendMode.NONE);
            layer.DrawRect(0, 0, W / 2, H / 2, 0.85f, 0.20f, 0.20f, 1f);              // TL red
            layer.DrawRect(W / 2, 0, W / 2, H / 2, 0.20f, 0.85f, 0.20f, 1f);           // TR green
            layer.DrawRect(0, H / 2, W / 2, H / 2, 0.20f, 0.35f, 0.95f, 1f);           // BL blue
            layer.DrawRect(W / 2, H / 2, W / 2, H / 2, 0.90f, 0.85f, 0.20f, 1f);       // BR yellow

            // --- 2) Stripe overlays (easy to spot scaling/cropping correctness) ---
            // vertical stripes in lower half
            for (int x = 0; x < W; x += 20)
            {
                var a = (x / 20) % 2 == 0 ? 0.35f : 0.08f;
                layer.DrawRect(x, H/2, 10, H / 2, 1f, 1f, 1f, a);
            }

            // horizontal stripes in upper half
            for (int y = 0; y < H/2; y += 20)
            {
                var a = ((y - H / 2) / 20) % 2 == 0 ? 0.35f : 0.08f;
                layer.DrawRect(0, y, W, 10, 1f, 1f, 1f, a);
            }

            // --- 3) Center crosshair (exact pixel) ---
            const float cx = W / 2f;
            const float cy = H / 2f;
            const int th = 4;
            const int pad = 4;
            layer.DrawRect(cx - (60+pad), cy - (th+pad), 120+pad*2, th*2+pad*2, 1f, 0f, 1f, 1f);
            layer.DrawRect(cx - (th+pad), cy - (60+pad), th*2+pad*2, 120+pad*2, 1f, 0f, 1f, 1f);
            layer.DrawRect(cx - (40+pad), cy - (60+pad), 80+pad*2, th*2+pad*2, 1f, 0f, 1f, 1f);

            layer.DrawRect(cx - 60, cy - th, 120, th*2, 1f, 1f, 1f, 1f);
            layer.DrawRect(cx - th, cy - 60, th*2, 120, 1f, 1f, 1f, 1f);
            layer.DrawRect(cx - 40, cy - 60, 80, th*2, 1f, 1f, 1f, 1f);

            // --- 4) Border outline (detect UV flip / off-by-one / scaling) ---
            DrawOutline(layer, new CodeDrawLayer.RectF(0, 0, W, H), new CodeDrawLayer.Rgba(1f, 1f, 1f, 1f), 3);

            // --- 5) Moving marker (helps confirm "latest frame" + no caching bugs) ---
            var mx = 400 + 140 * MathF.Sin(_t * 0.9f) + 230 * MathF.Cos(_t * 1.6f);
            var my = 250 +  70 * MathF.Cos(_t * 1.1f) + 150 * MathF.Sin(_t * 0.4f);
            layer.DrawRect(mx, my, 16, 16, 0f, 0f, 0f, 1f);
            layer.DrawRect(mx + 3, my + 3, 10, 10, 1f, 1f, 1f, 1f);

            layer.Render();
        };

        _winDst.OnUpdate = ctx =>
        {
            var dst = ctx.Win.Layer;
            if (dst is null || dst.IsDisposed) return;

            const int W = 800;
            const int H = 500;

            dst.EnsureCanvas(W, H);
            dst.Clear(0.06f, 0.06f, 0.06f, 1f);

            var src = _winSrc.Layer;
            if (src is null || src.IsDisposed) { dst.Render(); return; }

            // Background guide grid (visual alignment)
            dst.SetBlendMode(CodeDrawLayer.BlendMode.NONE);
            DrawGrid(dst, W, H, 40, new CodeDrawLayer.Rgba(0.15f, 0.15f, 0.15f, 1f));
            dst.SetBlendMode(CodeDrawLayer.BlendMode.SOURCE_OVER_ALPHA);

            // Draw separators
            dst.SetBlendMode(CodeDrawLayer.BlendMode.NONE);
            dst.DrawRect(0, 0, W, 4, 0f, 1f, 0f, 1f);
            dst.DrawRect(0, H - 4, W, 4, 0f, 1f, 0f, 1f);
            dst.SetBlendMode(CodeDrawLayer.BlendMode.SOURCE_OVER_ALPHA);

            // B) Full src -> region (aspect stretch is expected)
            var regionB = new CodeDrawLayer.RectF(30, 30, 220, 140);
            dst.DrawLayer(src, regionB);
            DrawOutline(dst, regionB, new CodeDrawLayer.Rgba(1f, 1f, 1f, 1f));

            // C) Crop TL quadrant -> box
            var cropTL = new CodeDrawLayer.RectF(0, 0, 400, 250);
            var dstC = new CodeDrawLayer.RectF(300, 30, 220, 140);
            dst.DrawLayer(src, cropTL, dstC);
            DrawOutline(dst, dstC, new CodeDrawLayer.Rgba(1f, 0.5f, 0.5f, 1f));

            // D) Crop stripe band (upper half) -> thin wide box
            var cropBand = new CodeDrawLayer.RectF(0, 0, 800, 120);
            var dstD = new CodeDrawLayer.RectF(30, 210, 490, 70);
            dst.DrawLayer(src, cropBand, dstD);
            DrawOutline(dst, dstD, new CodeDrawLayer.Rgba(0.6f, 1f, 0.6f, 1f));

            // E) Crop center square -> box (crosshair should be centered)
            var cropCenter = new CodeDrawLayer.RectF(300, 150, 200, 200);
            var dstE = new CodeDrawLayer.RectF(560, 30, 210, 210);
            dst.DrawLayer(src, cropCenter, dstE);
            DrawOutline(dst, dstE, new CodeDrawLayer.Rgba(0.6f, 0.8f, 1f, 1f));

            // F) Crop bottom-right quadrant -> box
            var cropBR = new CodeDrawLayer.RectF(400, 250, 400, 250);
            var dstF = new CodeDrawLayer.RectF(560, 270, 210, 190);
            dst.DrawLayer(src, cropBR, dstF);
            DrawOutline(dst, dstF, new CodeDrawLayer.Rgba(1f, 1f, 0.6f, 1f));

            // Legend markers (just colors, no text)
            dst.SetBlendMode(CodeDrawLayer.BlendMode.NONE);
            dst.DrawRect(30, 470, 20, 20, 1f, 1f, 1f, 1f);
            dst.DrawRect(60, 470, 20, 20, 1f, 0.5f, 0.5f, 1f);
            dst.DrawRect(90, 470, 20, 20, 0.6f, 1f, 0.6f, 1f);
            dst.DrawRect(120, 470, 20, 20, 0.6f, 0.8f, 1f, 1f);
            dst.DrawRect(150, 470, 20, 20, 1f, 1f, 0.6f, 1f);
            dst.SetBlendMode(CodeDrawLayer.BlendMode.SOURCE_OVER_ALPHA);

            dst.Render();
        };

        _winFull.OnUpdate = ctx =>
        {
            var dst = ctx.Win.Layer;
            if (dst is null || dst.IsDisposed) return;

            dst.EnsureCanvas(800, 500);
            dst.Clear(0.03f, 0.03f, 0.03f, 1f);

            var src = _winSrc.Layer;
            if (src is null || src.IsDisposed) { dst.Render(); return; }

            // --- A) Copy src fully, but through a mostly-desaturating shader ---
            dst.SetLayerBlitShader(_desatShader);
            dst.DrawLayer(src);
            dst.SetLayerBlitShader(null);

            // --- Now draw the SAME outlines used in _winDst, on top, in full color ---
            // (This makes it obvious where each crop/placement is coming from.)
            dst.SetBlendMode(CodeDrawLayer.BlendMode.SOURCE_OVER_ALPHA);

            var regionB = new CodeDrawLayer.RectF(0, 0, 800, 500);
            var srcC = new CodeDrawLayer.RectF(0, 0, 400, 250);
            var srcD = new CodeDrawLayer.RectF(0, 0, 800, 120);
            var srcE = new CodeDrawLayer.RectF(300, 150, 200, 200);
            var srcF = new CodeDrawLayer.RectF(400, 250, 400, 250);

            DrawOutline(dst, regionB, new CodeDrawLayer.Rgba(1f, 1f, 1f, 1f), 3);
            DrawOutline(dst, srcC,    new CodeDrawLayer.Rgba(1f, 0.5f, 0.5f, 1f), 3);
            DrawOutline(dst, srcD,    new CodeDrawLayer.Rgba(0.6f, 1f, 0.6f, 1f), 3);
            DrawOutline(dst, srcE,    new CodeDrawLayer.Rgba(0.6f, 0.8f, 1f, 1f), 3);
            DrawOutline(dst, srcF,    new CodeDrawLayer.Rgba(1f, 1f, 0.6f, 1f), 3);

            // Add small corner markers to show "top-left" of each box clearly
            MarkCorner(dst, regionB, new CodeDrawLayer.Rgba(1f, 1f, 1f, 1f));
            MarkCorner(dst, srcC,    new CodeDrawLayer.Rgba(1f, 0.5f, 0.5f, 1f));
            MarkCorner(dst, srcD,    new CodeDrawLayer.Rgba(0.6f, 1f, 0.6f, 1f));
            MarkCorner(dst, srcE,    new CodeDrawLayer.Rgba(0.6f, 0.8f, 1f, 1f));
            MarkCorner(dst, srcF,    new CodeDrawLayer.Rgba(1f, 1f, 0.6f, 1f));

            dst.Render();
        };
    }

    public void Dispose()
    {
        _winSrc.Dispose();
        _winDst.Dispose();
        _winFull.Dispose();
        _desatShader.Dispose();
    }

    private void WaitForClose()
    {
        _winSrc.WaitForClose();
        _winDst.WaitForClose();
        _winFull.WaitForClose();
    }

    private static void DrawOutline(CodeDrawLayer l, CodeDrawLayer.RectF r, CodeDrawLayer.Rgba c, float t = 2f)
    {
        l.SetBlendMode(CodeDrawLayer.BlendMode.SOURCE_OVER_ALPHA);
        l.DrawRect(r.X, r.Y, r.W, t, c.R, c.G, c.B, c.A);
        l.DrawRect(r.X, r.Y + r.H - t, r.W, t, c.R, c.G, c.B, c.A);
        l.DrawRect(r.X, r.Y, t, r.H, c.R, c.G, c.B, c.A);
        l.DrawRect(r.X + r.W - t, r.Y, t, r.H, c.R, c.G, c.B, c.A);
    }

    private static void DrawGrid(CodeDrawLayer l, int w, int h, int step, CodeDrawLayer.Rgba c)
    {
        for (int x = 0; x < w; x += step) l.DrawRect(x, 0, 1, h, c.R, c.G, c.B, c.A);
        for (int y = 0; y < h; y += step) l.DrawRect(0, y, w, 1, c.R, c.G, c.B, c.A);
    }

    private static void MarkCorner(CodeDrawLayer l, CodeDrawLayer.RectF r, CodeDrawLayer.Rgba c)
    {
        // top-left "L" marker
        l.SetBlendMode(CodeDrawLayer.BlendMode.SOURCE_OVER_ALPHA);
        l.DrawRect(r.X, r.Y, 14, 6, c.R, c.G, c.B, c.A);
        l.DrawRect(r.X, r.Y, 6, 14, c.R, c.G, c.B, c.A);
    }

    // ----------------------------------------
    // Custom blit shader: desaturate by uAmount
    // uAmount = 0 -> original color
    // uAmount = 1 -> fully grayscale
    // We will set it to ~0.85 for "mostly decolored".
    // ----------------------------------------
    private const string DesatShaderVs = """
        #version 330 core
        layout(location=0) in vec2 aPos;
        layout(location=1) in vec2 aUV;
        out vec2 vUV;
        void main(){
            vUV = aUV;
            gl_Position = vec4(aPos, 0.0, 1.0);
        }
        """;

    private const string DesatShaderFs = """
        #version 330 core
        in vec2 vUV;
        out vec4 FragColor;

        uniform sampler2D uTex;
        uniform float uAmount = 0.85; // 0..1

        void main(){
            vec4 c = texture(uTex, vUV);
            float lum = dot(c.rgb, vec3(0.2126, 0.7152, 0.0722));
            vec3 gray = vec3(lum);
            vec3 rgb = mix(c.rgb, gray, uAmount);
            FragColor = vec4(rgb, c.a);
        }
        """;
}
