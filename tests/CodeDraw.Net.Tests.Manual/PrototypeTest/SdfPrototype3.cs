using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;
using MarcoZechner.CodeDrawDotNet.Text;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet.HSV;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public class SdfPrototype3
{
    [ConstructorPrototype("SdfPrototype3")]
    public SdfPrototype3()
    {
        // ---- window loop ----
        using var app = CodeDrawHost.Start();
        var win = new CodeDrawWindow(1920, 1080, 50, 50, "SDF Prototype 3 - RoundedBox Rings");

        // ---- SDF: rounded rect in local space ----
        // Use whatever your SdfRoundedRectNode expects; common variants:
        // - RectWh (x,y,w,h) + Radius
        // - Rect (min/max) + Radius
        var rr = new SdfRoundedRectNode
        {
            Rect = new RectWh(-0, -0, 600, 400, OriginLocation.Center),
            Radius = 50f
        };

        var circle = new SdfCircleNode {
            Center = new Vector2(0, 200),
            Radius = 200,
        };

        // ---- Material: orange inside, blue outside + rings every "padding" px ----
        var padding = 60f;         // "every Padding pixels"
        var ringHalfWidth = 1f; // ring thickness control (tolerance around the ring center)
        var ringFeather = 0.9f;    // smoothness of ring edges

        var insideOrange0 = new ColorF(1.0f, 0.55f, 0.12f, 1.0f);    // near sd=0 (bright)
        var insideOrangeFar = ((ColorHsvF)new ColorF(0.35f, 0.15f, 0.05f, 1.0f)) with { V = 0, H = 0}; // far inside (dark)

        var outsideBlue0 = new ColorF(0.15f, 0.35f, 1.0f, 1.0f);    // near sd=0 (bright)
        var outsideBlueFar = new ColorF(0.03f, 0.06f, 0.20f, 1.0f); // far outside (dark)

        var ringBrightOrange = new ColorF(1.0f, 0.75f, 0.25f, 1.0f);
        var ringBrightBlue   = new ColorF(0.5f, 0.75f, 1.0f, 0.85f);
        
        // Base style can be anything; rules will overwrite anyway.
        // Still set feather so boundary AA is nice when rules don't override.
        var baseStyle = new DrawStyle(
            new Paint(insideOrange0, default(Stroke)),
            FeatherPx: 1.25f
        );
        
        var mat = new SdfMaterialDef(baseStyle);

        // IMPORTANT: your rules are "last wins overwrite".
        // So do:
        //  1) inside base overwrite
        //  2) outside base overwrite
        //  3) rings overwrite (placed last so they show up)
        //
        // Inside: sd < 0
        mat.Rules.Add(new SdfColorRuleDef(
            SdfRuleMode.SdLessThan,
            insideOrange0,
            0f, 0f,
            1.0f
        ));

        // Outside: sd > 0
        mat.Rules.Add(new SdfColorRuleDef(
            SdfRuleMode.SdGreaterThan,
            outsideBlue0,
            0f, 0f,
            1.5f
        ));
        
        // --- Darken with distance from 0 ---
        // Inside gradient: sd in [-falloffMax .. 0]
        // Near boundary (sd=0) => insideOrange0
        // Far inside   (sd=-falloffMax) => insideOrangeFar
        mat.Rules.Add(new SdfColorRuleDef(
            SdfRuleMode.Gradient,
            ColorA: insideOrangeFar,   // at sd=-falloffMax
            A: -300,
            B: 0f,
            FeatherPx: 0f,
            ColorB: insideOrange0      // at sd=0
        ));

        // Outside gradient: sd in [0 .. +falloffMax]
        // Near boundary (sd=0) => outsideBlue0
        // Far outside   (sd=+falloffMax) => outsideBlueFar
        mat.Rules.Add(new SdfColorRuleDef(
            SdfRuleMode.GradientStep,
            ColorA: outsideBlue0,      // at sd=0
            A: 0f,
            B: 800,
            FeatherPx: 0f,
            ColorB: outsideBlueFar,     // at sd=+falloffMax
            StepPx: padding
        ));

        // Rings: create repeated NearValue rules at +/-n*padding
        // sd is signed distance in *pixels*:
        //   inside ring centers: -padding, -2*padding, ...
        //   outside ring centers: +padding, +2*padding, ...
        //
        // NOTE: This is intentionally "more expensive" as you asked.
        // Choose how many rings to draw (based on your expected view).
        var ringCount = 20; // total rings each side (inside + outside)
        for (int i = 1; i <= ringCount; i++)
        {
            var d = i * padding;

            // Outside ring at +d
            // mat.Rules.Add(new SdfColorRuleDef(
            //     SdfRuleMode.NearValue,
            //     ringBrightBlue,
            //     d,               // A = target sd value
            //     ringHalfWidth,   // B = tolerance
            //     ringFeather
            // ));

            // Inside ring at -d
            mat.Rules.Add(new SdfColorRuleDef(
                SdfRuleMode.NearValue,
                ringBrightOrange,
                -d,
                ringHalfWidth,
                ringFeather
            ));
        }
        
        var union = new SdfUnionNode()
        {
            Children = [rr, circle]
        };

        // Tag the SDF with the material
        ISdf2Node root = new SdfMaterialNode
        {
            Child = union,
            Material = mat
        };

        // Optional: HUD
        var baseFont = FontRef.FromFile(
            @"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf"
        );

        var hudStyle = new TextStyle
        {
            Font = baseFont.WithVariant(FontVariant.Regular),
            SizePx = 16,
            Align = TextAlign.Left,
            VAlign = TextVAlign.Top,
            Color = new ColorF(1f, 1f, 1f, 0.95f),
            Background = new ColorF(0f, 0f, 0f, 0.55f),
            MonospaceSnapLineAlignToCells = true
        };

        var time = 0f;

        win.OnUpdate += ctx =>
        {
            var layer = ctx.Win.Layer;
            time += ctx.DeltaSeconds;

            layer.Clear(0.08f, 0.08f, 0.10f, 1f);

            float speedLoopsPerSecond = 0.15f; // tweak
            float t = time * speedLoopsPerSecond;

            rr.Rect = rr.Rect with {Size = new Vector2(rr.Rect.Width, 400f + 100f * MathF.Sin(t*20))};
            
            circle.Center = RoundedRectOutlinePoint(t, rr.Rect.Width, rr.Rect.Height, rr.Radius);
            
            using (layer.ScopeTranslate(layer.Width / 2f, layer.Height / 2f))
            {
                layer.DrawSdf(
                    root,
                    style: null,
                    forceStrokeOnly: false,
                    drawAreaOverride: new SdfDrawAreaOverride(layer.FullRect, SdfDrawAreaMode.Replace)
                );
            }

            var hud = $"padding={padding:0.0}px  rings={ringCount}  ringHalfWidth={ringHalfWidth:0.00}px";
            var sz = layer.MeasureText(hud, hudStyle);
            layer.DrawText(hud, 14, layer.Height - 14 - sz.Y, hudStyle);

            layer.Render();
        };

        app.WaitForClose();
    }
    
    static float Frac(float x) => x - MathF.Floor(x);

    // Returns a point on the outline for t in [0,1).
    static Vector2 RoundedRectOutlinePoint(float t01, float w, float h, float r)
    {
        r = MathF.Max(0f, MathF.Min(r, 0.5f * MathF.Min(w, h)));

        float hx = 0.5f * w;
        float hy = 0.5f * h;

        float a = MathF.Max(0f, w - 2f * r); // horizontal straight length (top/bottom)
        float b = MathF.Max(0f, h - 2f * r); // vertical straight length (left/right)

        // Perimeter (4 straights + 4 quarter arcs)
        float L =
            2f * a +
            2f * b +
            2f * MathF.PI * r;

        if (L <= 1e-6f)
            return Vector2.Zero;

        float s = Frac(t01) * L;

        // Start at: top edge, moving right, at ( -hx + r, +hy )
        // Segment order:
        // 0) top straight (left->right)
        // 1) top-right arc (90°)
        // 2) right straight (top->bottom)
        // 3) bottom-right arc (90°)
        // 4) bottom straight (right->left)
        // 5) bottom-left arc (90°)
        // 6) left straight (bottom->top)
        // 7) top-left arc (90°)

        // 0) top straight
        if (s < a)
            return new Vector2(-hx + r + s, +hy);
        s -= a;

        // 1) top-right arc: center ( +hx - r, +hy - r ), angle from +90° to 0° (clockwise)
        float q = 0.5f * MathF.PI * r;
        if (s < q)
        {
            float ang = (0.5f * MathF.PI) - (s / r); // decreases
            var c = new Vector2(+hx - r, +hy - r);
            return c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r;
        }
        s -= q;

        // 2) right straight
        if (s < b)
            return new Vector2(+hx, +hy - r - s);
        s -= b;

        // 3) bottom-right arc: center ( +hx - r, -hy + r ), angle from 0° to -90°
        if (s < q)
        {
            float ang = 0f - (s / r);
            var c = new Vector2(+hx - r, -hy + r);
            return c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r;
        }
        s -= q;

        // 4) bottom straight (right->left)
        if (s < a)
            return new Vector2(+hx - r - s, -hy);
        s -= a;

        // 5) bottom-left arc: center ( -hx + r, -hy + r ), angle from -90° to -180°
        if (s < q)
        {
            float ang = (-0.5f * MathF.PI) - (s / r);
            var c = new Vector2(-hx + r, -hy + r);
            return c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r;
        }
        s -= q;

        // 6) left straight (bottom->top)
        if (s < b)
            return new Vector2(-hx, -hy + r + s);
        s -= b;

        // 7) top-left arc: center ( -hx + r, +hy - r ), angle from -180° to -270° (== +180° to +90°)
        {
            float ang = (-MathF.PI) - (s / r);
            var c = new Vector2(-hx + r, +hy - r);
            return c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r;
        }
    }
}