using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using CodeDrawLayer = MarcoZechner.CodeDrawDotNet.DrawLayer.CodeDrawLayer;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public sealed class Prototype2
{
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

    private static bool Hit(Rect r, float mx, float my)
        => mx >= r.Left && mx <= r.Right && my >= r.Top && my <= r.Bottom; //TODO: use Contains from Rect

    private static HoverRegion ComputeHoverInWinDst(float mx, float my)
    {
        // These MUST match the rectangles in your _winDst.OnUpdate
        var regionB  = new RectWh(30, 30, 220, 140);
        var dstC     = new RectWh(300, 30, 220, 140);
        var dstD     = new RectWh(30, 210, 490, 70);
        var dstE     = new RectWh(560, 30, 210, 210);
        var dstF     = new RectWh(560, 270, 210, 190);

        // Order matters if you ever overlap.
        if (Hit(regionB, mx, my)) return HoverRegion.B_FULL;
        if (Hit(dstC,    mx, my)) return HoverRegion.C_TL_QUADRANT;
        if (Hit(dstD,    mx, my)) return HoverRegion.D_BAND;
        if (Hit(dstE,    mx, my)) return HoverRegion.E_CENTER;
        if (Hit(dstF,    mx, my)) return HoverRegion.F_BR_QUADRANT;

        return HoverRegion.NONE;
    }

    [ConstructorPrototype(2)]
    public Prototype2()
    {
        using var app = CodeDrawHost.Started();
        
        var winSrc = new CodeDrawWindow(800, 500, 50, 120, "2B: Source (Pattern Atlas)");
        var winDst = new CodeDrawWindow(800, 500, 850, 120, "2B: Dest (Crop/Place Tests)");
        var winFull = new CodeDrawWindow(800, 500, 1650, 120, "2B: Full (Copy Src fully, mostly desaturated)");

        var desatCopyShader = CodeDrawShader.CsProject("desat", "PrototypeTest/shaders");
        var orbitShader = CodeDrawShader.CsProject("orbitDots", "PrototypeTest/shaders");

        winSrc.OnStart = w => Console.WriteLine($"2B Src started (id={w.WindowId})");
        winDst.OnStart = w => Console.WriteLine($"2B Dst started (id={w.WindowId})");
        winFull.OnStart = w => Console.WriteLine($"2B Full started (id={w.WindowId})");
        winSrc.OnClose = w => Console.WriteLine($"2B Src closed (id={w.WindowId})");
        winDst.OnClose = w => Console.WriteLine($"2B Dst closed (id={w.WindowId})");
        winFull.OnClose = w => Console.WriteLine($"2B Full closed (id={w.WindowId})");

        winSrc.TransparentAlpha = true;
        winDst.TransparentAlpha = true;
        winFull.TransparentAlpha = true;
        
        app.Input.OnKeyDown += (win, key, _) =>
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


        winSrc.OnUpdate = ctx =>
        {
            _t += ctx.DeltaSeconds;

            var layer = ctx.Win.Layer;
            if (layer.IsDisposed) return;

            const int W = 800;
            const int H = 500;
            const int HALF_W = W/2;
            const int HALF_H = H/2;

            layer.RequestLayerSize(W, H);
            layer.Clear(0.02f, 0.02f, 0.02f, 1f);

            // --- 1) Quadrants (unique colors) ---
            layer.SetBlendMode(BlendMode.NONE);
            layer.DrawDebugRect(0, 0, HALF_W, HALF_H, 0.85f, 0.20f, 0.20f, 1f);              // TL red
            layer.DrawDebugRect(HALF_W, 0, HALF_W, HALF_H, 0.20f, 0.85f, 0.20f, 1f);           // TR green
            layer.DrawDebugRect(0, HALF_H, HALF_W, HALF_H, 0.20f, 0.35f, 0.95f, 1f);           // BL blue
            layer.DrawDebugRect(HALF_W, HALF_H, HALF_W, HALF_H, 0.90f, 0.85f, 0.20f, 1f);       // BR yellow

            // --- 2) Stripe overlays (easy to spot scaling/cropping correctness) ---
            // vertical stripes in lower half
            for (var x = 0; x < W; x += 20)
            {
                var a = (x / 20) % 2 == 0 ? 0.35f : 0.08f;
                layer.DrawDebugRect(x, HALF_H, 10, HALF_H, 1f, 1f, 1f, a);
            }

            // horizontal stripes in upper half
            for (var y = 0; y < HALF_H; y += 20)
            {
                var a = ((y - HALF_H) / 20) % 2 == 0 ? 0.35f : 0.08f;
                layer.DrawDebugRect(0, y, W, 10, 1f, 1f, 1f, a);
            }

            // --- 3) Center crosshair (exact pixel) ---
            const float CX = HALF_W;
            const float CY = HALF_H;
            const int TH = 4;
            const int PAD = 4;
            layer.DrawDebugRect(CX - (60+PAD), CY - (TH+PAD), 120+PAD*2, TH*2+PAD*2, 1f, 0f, 1f, 1f);
            layer.DrawDebugRect(CX - (TH+PAD), CY - (60+PAD), TH*2+PAD*2, 120+PAD*2, 1f, 0f, 1f, 1f);
            layer.DrawDebugRect(CX - (40+PAD), CY - (60+PAD), 80+PAD*2, TH*2+PAD*2, 1f, 0f, 1f, 1f);

            layer.DrawDebugRect(CX - 60, CY - TH, 120, TH*2, 1f, 1f, 1f, 1f);
            layer.DrawDebugRect(CX - TH, CY - 60, TH*2, 120, 1f, 1f, 1f, 1f);
            layer.DrawDebugRect(CX - 40, CY - 60, 80, TH*2, 1f, 1f, 1f, 1f);

            // --- 4) Border outline (detect UV flip / off-by-one / scaling) ---
            DrawOutline(layer, new RectWh(0, 0, W, H), new ColorF(1f, 1f, 1f, 1f), 3);

            // --- 5) Moving marker (helps confirm "latest frame" + no caching bugs) ---
            var mx = 400 + 140 * MathF.Sin(_t * 0.9f) + 230 * MathF.Cos(_t * 1.6f);
            var my = 250 +  70 * MathF.Cos(_t * 1.1f) + 150 * MathF.Sin(_t * 0.4f);
            layer.DrawDebugRect(mx, my, 16, 16, 0f, 0f, 0f, 1f);
            layer.DrawDebugRect(mx + 3, my + 3, 10, 10, 1f, 1f, 1f, 1f);

            layer.CustomRect(
                layer.FullRect,
                shader: orbitShader,
                uniforms: Uniforms.Of(
                    UniformValue.Float("uTime", layer.LayerAliveForSeconds()),
                    UniformValue.Float4("uColor", 1,1,1,1),
                    UniformValue.Float("uRadius1", 20),
                    UniformValue.Float("uRadius2", 200f),
                    UniformValue.Float("uPeriod",  20),
                    UniformValue.Float("uOffset",  0)
                )
            );

            DrawOrbitingDots(layer, 400, 250, 20, 200, 20, 5f, new ColorF(1f, 0.5f, 0f, MathF.Sin(_t * 1.6f) * 0.5f + 0.5f));

            layer.Render();
        };

        void DrawOrbitingDots(CodeDrawLayer layer, int centerX, int centerY, int radiusDot, int radiusOrbit, float period, float timeOffset, ColorF color)
        {
            var size = radiusOrbit * 2 + radiusDot * 2;
            
            layer.CustomRect(
                new RectWh<int>(centerX-size/2, centerY-size/2, size, size),
                shader: orbitShader,
                uniforms: Uniforms.Of(
                    UniformValue.Float("uTime", layer.LayerAliveForSeconds()),
                    UniformValue.Float4("uColor", color.R, color.G, color.B, color.A),
                    UniformValue.Float("uRadius1", radiusDot),
                    UniformValue.Float("uRadius2", radiusOrbit),
                    UniformValue.Float("uPeriod",  period),
                    UniformValue.Float("uOffset",  timeOffset)
                )
            );
        }

        winDst.OnUpdate = ctx =>
        {
            var dst = ctx.Win.Layer;
            if (dst.IsDisposed) return;

            const int W = 800;
            const int H = 500;

            dst.RequestLayerSize(W, H);
            dst.Clear(0.06f, 0.06f, 0.06f, 1f);

            var src = winSrc.Layer;
            if (src.IsDisposed) { dst.Render(); return; }

            var (mxCanvas, myCanvas) = dst.TransformPointFrom(ctx.Win, (float)ctx.Input.MouseX, (float)ctx.Input.MouseY);

            var hover = ComputeHoverInWinDst(mxCanvas, myCanvas);
            Volatile.Write(ref _hoverRegion, (int)hover);
            // Console.WriteLine($"Dst mouse win=({ctx.Input.MouseX:0.0},{ctx.Input.MouseY:0.0}) canvas=({mxCanvas:0.0},{myCanvas:0.0}) winSize=({ctx.Win.Width},{ctx.Win.Height})");

            dst.SetBlendMode(BlendMode.NONE);
            DrawGrid(dst, W, H, 40, new ColorF(0.15f, 0.15f, 0.15f, 1f));
            dst.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);

            dst.SetBlendMode(BlendMode.NONE);
            dst.DrawDebugRect(0, 0, W, 4, 0f, 1f, 0f, 1f);
            dst.DrawDebugRect(0, H - 4, W, 4, 0f, 1f, 0f, 1f);
            dst.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);

            var regionB = new RectWh(30, 30, 220, 140);
            dst.DrawLayer(src, regionB);
            DrawOutline(dst, regionB, new ColorF(1f, 1f, 1f, 1f));

            var cropTl = new RectWh(0, 0, 400, 250);
            var dstC = new RectWh(300, 30, 220, 140);
            dst.DrawLayer(src, cropTl, dstC);
            DrawOutline(dst, dstC, new ColorF(1f, 0.5f, 0.5f, 1f));

            var cropBand = new RectWh(0, 0, 800, 120);
            var dstD = new RectWh(30, 210, 490, 70);
            dst.DrawLayer(src, cropBand, dstD);
            DrawOutline(dst, dstD, new ColorF(0.6f, 1f, 0.6f, 1f));

            var cropCenter = new RectWh(300, 150, 200, 200);
            var dstE = new RectWh(560, 30, 210, 210);
            dst.DrawLayer(src, cropCenter, dstE);
            DrawOutline(dst, dstE, new ColorF(0.6f, 0.8f, 1f, 1f));

            var cropBr = new RectWh(400, 250, 400, 250);
            var dstF = new RectWh(560, 270, 210, 190);
            dst.DrawLayer(src, cropBr, dstF);
            DrawOutline(dst, dstF, new ColorF(1f, 1f, 0.6f, 1f));

            dst.SetBlendMode(BlendMode.NONE);
            dst.DrawDebugRect(30, 470, 20, 20, 1f, 1f, 1f, 1f);
            dst.DrawDebugRect(60, 470, 20, 20, 1f, 0.5f, 0.5f, 1f);
            dst.DrawDebugRect(90, 470, 20, 20, 0.6f, 1f, 0.6f, 1f);
            dst.DrawDebugRect(120, 470, 20, 20, 0.6f, 0.8f, 1f, 1f);
            dst.DrawDebugRect(150, 470, 20, 20, 1f, 1f, 0.6f, 1f);
            dst.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);

            dst.Render();
        };

        winFull.OnUpdate = ctx =>
        {
            var dst = ctx.Win.Layer;
            if (dst.IsDisposed) return;

            dst.RequestLayerSize(800, 500);
            dst.Clear(0.03f, 0.03f, 0.03f, 1f);

            var src = winSrc.Layer;
            if (src.IsDisposed) { dst.Render(); return; }

            dst.DrawLayer(src, desatCopyShader);

            dst.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);

            // These are SOURCE-SPACE rects (as you already did)
            var regionB = new RectWh(0, 0, 800, 500);
            var srcC    = new RectWh(0, 0, 400, 250);
            var srcD    = new RectWh(0, 0, 800, 120);
            var srcE    = new RectWh(300, 150, 200, 200);
            var srcF    = new RectWh(400, 250, 400, 250);

            var hover = (HoverRegion)Volatile.Read(ref _hoverRegion);

            const int BASE_T = 3;
            const int HOT_T = 20; // thicker when hovered

            DrawOutline(dst, regionB, new ColorF(1f, 1f, 1f, 1f), hover == HoverRegion.B_FULL ? HOT_T : BASE_T);
            DrawOutline(dst, srcC,    new ColorF(1f, 0.5f, 0.5f, 1f), hover == HoverRegion.C_TL_QUADRANT ? HOT_T : BASE_T);
            DrawOutline(dst, srcD,    new ColorF(0.6f, 1f, 0.6f, 1f), hover == HoverRegion.D_BAND ? HOT_T : BASE_T);
            DrawOutline(dst, srcE,    new ColorF(0.6f, 0.8f, 1f, 1f), hover == HoverRegion.E_CENTER ? HOT_T : BASE_T);
            DrawOutline(dst, srcF,    new ColorF(1f, 1f, 0.6f, 1f), hover == HoverRegion.F_BR_QUADRANT ? HOT_T : BASE_T);

            MarkCorner(dst, regionB, new ColorF(1f, 1f, 1f, 1f));
            MarkCorner(dst, srcC,    new ColorF(1f, 0.5f, 0.5f, 1f));
            MarkCorner(dst, srcD,    new ColorF(0.6f, 1f, 0.6f, 1f));
            MarkCorner(dst, srcE,    new ColorF(0.6f, 0.8f, 1f, 1f));
            MarkCorner(dst, srcF,    new ColorF(1f, 1f, 0.6f, 1f));

            dst.Render();
        };
        
        app.WaitForClose();
    }

    private static void DrawOutline(CodeDrawLayer l, Rect r, ColorF c, float t = 2f)
    {
        l.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);
        l.DrawDebugRect(r.Left, r.Top, r.Width, t, c.R, c.G, c.B, c.A);
        l.DrawDebugRect(r.Left, r.Top + r.Height - t, r.Width, t, c.R, c.G, c.B, c.A);
        l.DrawDebugRect(r.Left, r.Top, t, r.Height, c.R, c.G, c.B, c.A);
        l.DrawDebugRect(r.Left + r.Width - t, r.Top, t, r.Height, c.R, c.G, c.B, c.A);
    }

    private static void DrawGrid(CodeDrawLayer l, int w, int h, int step, ColorF c)
    {
        for (var x = 0; x < w; x += step) l.DrawDebugRect(x, 0, 1, h, c.R, c.G, c.B, c.A);
        for (var y = 0; y < h; y += step) l.DrawDebugRect(0, y, w, 1, c.R, c.G, c.B, c.A);
    }

    private static void MarkCorner(CodeDrawLayer l, Rect r, ColorF c)
    {
        // top-left "L" marker
        l.SetBlendMode(BlendMode.SOURCE_OVER_ALPHA);
        l.DrawDebugRect(r.Left, r.Top, 14, 6, c.R, c.G, c.B, c.A);
        l.DrawDebugRect(r.Left, r.Top, 6, 14, c.R, c.G, c.B, c.A);
    }
}
