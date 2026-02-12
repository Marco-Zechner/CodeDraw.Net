using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using Silk.NET.GLFW;
using CodeDrawLayer = MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.CodeDrawLayer;

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

    private float _t;

    private enum HoverRegion
    {
        NONE,
        B_FULL,
        C_TL_QUADRANT,
        D_BAND,
        E_CENTER,
        F_BR_QUADRANT,
    }

    private int _hoverRegion = (int)HoverRegion.NONE;

    private static bool Hit(RectF r, float mx, float my)
        => mx >= r.X && mx <= r.X2 && my >= r.Y && my <= r.Y2;

    private static HoverRegion ComputeHoverInWinDst(float mx, float my)
    {
        // These MUST match the rectangles in your _winDst.OnUpdate
        var regionB  = new RectF(30, 30, 220, 140);
        var dstC     = new RectF(300, 30, 220, 140);
        var dstD     = new RectF(30, 210, 490, 70);
        var dstE     = new RectF(560, 30, 210, 210);
        var dstF     = new RectF(560, 270, 210, 190);

        // Order matters if you ever overlap.
        if (Hit(regionB, mx, my)) return HoverRegion.B_FULL;
        if (Hit(dstC,    mx, my)) return HoverRegion.C_TL_QUADRANT;
        if (Hit(dstD,    mx, my)) return HoverRegion.D_BAND;
        if (Hit(dstE,    mx, my)) return HoverRegion.E_CENTER;
        if (Hit(dstF,    mx, my)) return HoverRegion.F_BR_QUADRANT;

        return HoverRegion.NONE;
    }

    public Prototype2(SharedGlfwHost host)
    {
        _winSrc = new CodeDrawWindow(host, 800, 500, 50, 120, "2B: Source (Pattern Atlas)");
        _winDst = new CodeDrawWindow(host, 800, 500, 850, 120, "2B: Dest (Crop/Place Tests)");
        _winFull = new CodeDrawWindow(host, 800, 500, 1650, 120, "2B: Full (Copy Src fully, mostly desaturated)");

        var desatCopyShader = CustomShader.CsProject("desat", "PrototypeTest/shaders");
        var orbitShader = CustomShader.CsProject("orbitDots", "PrototypeTest/shaders");

        _winSrc.OnStart = w => Console.WriteLine($"2B Src started (id={w.WindowId})");
        _winDst.OnStart = w => Console.WriteLine($"2B Dst started (id={w.WindowId})");
        _winFull.OnStart = w => Console.WriteLine($"2B Full started (id={w.WindowId})");
        _winSrc.OnClose = w => Console.WriteLine($"2B Src closed (id={w.WindowId})");
        _winDst.OnClose = w => Console.WriteLine($"2B Dst closed (id={w.WindowId})");
        _winFull.OnClose = w => Console.WriteLine($"2B Full closed (id={w.WindowId})");

        host.Input.OnKeyDown += (win, key, mods) =>
        {
            switch (key)
            {
                case Keys.Escape:
                    win.Close();
                    break;
                case Keys.F11:
                    win.Settings = win.Settings with { State = win.Settings.State == WindowState.Windowed ? WindowState.BorderlessFullscreen : WindowState.Windowed };
                    break;
            }
        };


        _winSrc.OnUpdate = ctx =>
        {
            _t += ctx.DeltaSeconds;

            var layer = ctx.Win.Layer;
            if (layer is null || layer.IsDisposed) return;

            const int W = 800;
            const int H = 500;

            layer.RequestLayerSize(W, H);
            layer.Clear(0.02f, 0.02f, 0.02f, 1f);

            // --- 1) Quadrants (unique colors) ---
            layer.SetBlendMode(BlendMode.NONE);
            layer.DrawRect(0, 0, W / 2, H / 2, 0.85f, 0.20f, 0.20f, 1f);              // TL red
            layer.DrawRect(W / 2, 0, W / 2, H / 2, 0.20f, 0.85f, 0.20f, 1f);           // TR green
            layer.DrawRect(0, H / 2, W / 2, H / 2, 0.20f, 0.35f, 0.95f, 1f);           // BL blue
            layer.DrawRect(W / 2, H / 2, W / 2, H / 2, 0.90f, 0.85f, 0.20f, 1f);       // BR yellow

            // --- 2) Stripe overlays (easy to spot scaling/cropping correctness) ---
            // vertical stripes in lower half
            for (var x = 0; x < W; x += 20)
            {
                var a = (x / 20) % 2 == 0 ? 0.35f : 0.08f;
                layer.DrawRect(x, H/2, 10, H / 2, 1f, 1f, 1f, a);
            }

            // horizontal stripes in upper half
            for (var y = 0; y < H/2; y += 20)
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
            DrawOutline(layer, new RectF(0, 0, W, H), new Rgba(1f, 1f, 1f, 1f), 3);

            // --- 5) Moving marker (helps confirm "latest frame" + no caching bugs) ---
            var mx = 400 + 140 * MathF.Sin(_t * 0.9f) + 230 * MathF.Cos(_t * 1.6f);
            var my = 250 +  70 * MathF.Cos(_t * 1.1f) + 150 * MathF.Sin(_t * 0.4f);
            layer.DrawRect(mx, my, 16, 16, 0f, 0f, 0f, 1f);
            layer.DrawRect(mx + 3, my + 3, 10, 10, 1f, 1f, 1f, 1f);

            layer.CustomDrawRect(
                shader: orbitShader,
                uniforms: Uniforms.Of(
                    UniformValue.Float2("uPos", 200, 150),
                    UniformValue.Float2("uSize", 800, 500),
                    UniformValue.Float("uTime", layer.LayerAliveForSeconds()),
                    UniformValue.Float4("uColor", 1,1,1,1),
                    UniformValue.Float("uRadius1", 20),
                    UniformValue.Float("uRadius2", 200f),
                    UniformValue.Float("uPeriod",  20),
                    UniformValue.Float("uOffset",  0)
                )
            );

            DrawOrbitingDots(layer, 200, 150, 20f, 200f, 20, 5f, new Rgba(1f, 0.5f, 0f, MathF.Sin(_t * 1.6f) * 0.5f + 0.5f));

            layer.Render();
        };

        void DrawOrbitingDots(CodeDrawLayer layer, float centerX, float centerY, float radiusDot, float radiusOrbit, float period, float timeOffset, Rgba color)
        {
            layer.CustomDrawRect(
                shader: orbitShader,
                uniforms: Uniforms.Of(
                    UniformValue.Float2("uPos", centerX, centerY),
                    UniformValue.Float2("uSize", layer.Width, layer.Height),
                    UniformValue.Float("uTime", layer.LayerAliveForSeconds()),
                    UniformValue.Float4("uColor", color.R, color.G, color.B, color.A),
                    UniformValue.Float("uRadius1", radiusDot),
                    UniformValue.Float("uRadius2", radiusOrbit),
                    UniformValue.Float("uPeriod",  period),
                    UniformValue.Float("uOffset",  timeOffset)
                )
            );
        }

        _winDst.OnUpdate = ctx =>
        {
            var dst = ctx.Win.Layer;
            if (dst is null || dst.IsDisposed) return;

            const int W = 800;
            const int H = 500;

            dst.RequestLayerSize(W, H);
            dst.Clear(0.06f, 0.06f, 0.06f, 1f);

            var src = _winSrc.Layer;
            if (src is null || src.IsDisposed) { dst.Render(); return; }

            var (mxCanvas, myCanvas) = dst.TransformPointFrom(ctx.Win, (float)ctx.Input.MouseX, (float)ctx.Input.MouseY);

            var hover = ComputeHoverInWinDst(mxCanvas, myCanvas);
            Volatile.Write(ref _hoverRegion, (int)hover);
            // Console.WriteLine($"Dst mouse win=({ctx.Input.MouseX:0.0},{ctx.Input.MouseY:0.0}) canvas=({mxCanvas:0.0},{myCanvas:0.0}) winSize=({ctx.Win.Width},{ctx.Win.Height})");

            dst.SetBlendMode(BlendMode.NONE);
            DrawGrid(dst, W, H, 40, new Rgba(0.15f, 0.15f, 0.15f, 1f));
            dst.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);

            dst.SetBlendMode(BlendMode.NONE);
            dst.DrawRect(0, 0, W, 4, 0f, 1f, 0f, 1f);
            dst.DrawRect(0, H - 4, W, 4, 0f, 1f, 0f, 1f);
            dst.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);

            var regionB = new RectF(30, 30, 220, 140);
            dst.DrawLayer(src, regionB);
            DrawOutline(dst, regionB, new Rgba(1f, 1f, 1f, 1f));

            var cropTl = new RectF(0, 0, 400, 250);
            var dstC = new RectF(300, 30, 220, 140);
            dst.DrawLayer(src, cropTl, dstC);
            DrawOutline(dst, dstC, new Rgba(1f, 0.5f, 0.5f, 1f));

            var cropBand = new RectF(0, 0, 800, 120);
            var dstD = new RectF(30, 210, 490, 70);
            dst.DrawLayer(src, cropBand, dstD);
            DrawOutline(dst, dstD, new Rgba(0.6f, 1f, 0.6f, 1f));

            var cropCenter = new RectF(300, 150, 200, 200);
            var dstE = new RectF(560, 30, 210, 210);
            dst.DrawLayer(src, cropCenter, dstE);
            DrawOutline(dst, dstE, new Rgba(0.6f, 0.8f, 1f, 1f));

            var cropBr = new RectF(400, 250, 400, 250);
            var dstF = new RectF(560, 270, 210, 190);
            dst.DrawLayer(src, cropBr, dstF);
            DrawOutline(dst, dstF, new Rgba(1f, 1f, 0.6f, 1f));

            dst.SetBlendMode(BlendMode.NONE);
            dst.DrawRect(30, 470, 20, 20, 1f, 1f, 1f, 1f);
            dst.DrawRect(60, 470, 20, 20, 1f, 0.5f, 0.5f, 1f);
            dst.DrawRect(90, 470, 20, 20, 0.6f, 1f, 0.6f, 1f);
            dst.DrawRect(120, 470, 20, 20, 0.6f, 0.8f, 1f, 1f);
            dst.DrawRect(150, 470, 20, 20, 1f, 1f, 0.6f, 1f);
            dst.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);

            dst.Render();
        };

        _winFull.OnUpdate = ctx =>
        {
            var dst = ctx.Win.Layer;
            if (dst is null || dst.IsDisposed) return;

            dst.RequestLayerSize(800, 500);
            dst.Clear(0.03f, 0.03f, 0.03f, 1f);

            var src = _winSrc.Layer;
            if (src is null || src.IsDisposed) { dst.Render(); return; }

            dst.DrawLayer(src, desatCopyShader);

            dst.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);

            // These are SOURCE-SPACE rects (as you already did)
            var regionB = new RectF(0, 0, 800, 500);
            var srcC    = new RectF(0, 0, 400, 250);
            var srcD    = new RectF(0, 0, 800, 120);
            var srcE    = new RectF(300, 150, 200, 200);
            var srcF    = new RectF(400, 250, 400, 250);

            var hover = (HoverRegion)Volatile.Read(ref _hoverRegion);

            const int baseT = 3;
            const int hotT = 20; // thicker when hovered

            DrawOutline(dst, regionB, new Rgba(1f, 1f, 1f, 1f), hover == HoverRegion.B_FULL ? hotT : baseT);
            DrawOutline(dst, srcC,    new Rgba(1f, 0.5f, 0.5f, 1f), hover == HoverRegion.C_TL_QUADRANT ? hotT : baseT);
            DrawOutline(dst, srcD,    new Rgba(0.6f, 1f, 0.6f, 1f), hover == HoverRegion.D_BAND ? hotT : baseT);
            DrawOutline(dst, srcE,    new Rgba(0.6f, 0.8f, 1f, 1f), hover == HoverRegion.E_CENTER ? hotT : baseT);
            DrawOutline(dst, srcF,    new Rgba(1f, 1f, 0.6f, 1f), hover == HoverRegion.F_BR_QUADRANT ? hotT : baseT);

            MarkCorner(dst, regionB, new Rgba(1f, 1f, 1f, 1f));
            MarkCorner(dst, srcC,    new Rgba(1f, 0.5f, 0.5f, 1f));
            MarkCorner(dst, srcD,    new Rgba(0.6f, 1f, 0.6f, 1f));
            MarkCorner(dst, srcE,    new Rgba(0.6f, 0.8f, 1f, 1f));
            MarkCorner(dst, srcF,    new Rgba(1f, 1f, 0.6f, 1f));

            dst.Render();
        };
    }

    public void Dispose()
    {
        _winSrc.Dispose();
        _winDst.Dispose();
        _winFull.Dispose();
    }

    private void WaitForClose()
    {
        _winSrc.WaitForClose();
        _winDst.WaitForClose();
        _winFull.WaitForClose();
    }

    private static void DrawOutline(CodeDrawLayer l, RectF r, Rgba c, float t = 2f)
    {
        l.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);
        l.DrawRect(r.X, r.Y, r.W, t, c.R, c.G, c.B, c.A);
        l.DrawRect(r.X, r.Y + r.H - t, r.W, t, c.R, c.G, c.B, c.A);
        l.DrawRect(r.X, r.Y, t, r.H, c.R, c.G, c.B, c.A);
        l.DrawRect(r.X + r.W - t, r.Y, t, r.H, c.R, c.G, c.B, c.A);
    }

    private static void DrawGrid(CodeDrawLayer l, int w, int h, int step, Rgba c)
    {
        for (var x = 0; x < w; x += step) l.DrawRect(x, 0, 1, h, c.R, c.G, c.B, c.A);
        for (var y = 0; y < h; y += step) l.DrawRect(0, y, w, 1, c.R, c.G, c.B, c.A);
    }

    private static void MarkCorner(CodeDrawLayer l, RectF r, Rgba c)
    {
        // top-left "L" marker
        l.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);
        l.DrawRect(r.X, r.Y, 14, 6, c.R, c.G, c.B, c.A);
        l.DrawRect(r.X, r.Y, 6, 14, c.R, c.G, c.B, c.A);
    }
}
