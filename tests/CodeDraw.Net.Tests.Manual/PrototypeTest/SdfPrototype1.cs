using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;
using MarcoZechner.CodeDrawDotNet.Text;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public class SdfPrototype1
{
    [ConstructorPrototype("SdfPrototype1")]
    public SdfPrototype1()
    {
        var ups = 0f;
        var updates = 0;
        var upsAccum = 0f;

        // --- Styles ---
        var baseFont = FontRef.FromFile(
            @"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf"
        );
        
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
        
        using var app = CodeDrawHost.Started();

        var win = new CodeDrawWindow(900, 600, 50, 50, "SDF Prototype 1");
        var time = 0f;

        // ---- shared debug styling ----
        static ColorF DebugWhite(float a = 0.5f) => ((ColorF)Colors.WHITE) with { A = a };

        win.OnUpdate += ctx =>
        {
            var layer = ctx.Win.Layer;
            
            var dt = ctx.DeltaSeconds;

            updates++;
            upsAccum += dt;
            if (upsAccum >= 0.25f)
            {
                ups = updates / upsAccum;
                updates = 0;
                upsAccum = 0f;
            }
            
            time += ctx.DeltaSeconds;

            layer.Clear(0.1f, 0.1f, 0.12f, 1f);

            // ---- basic styles ----
            var fillBlue  = new Paint(new ColorF(0.2f, 0.5f, 1f, 1f), default);
            var fillRed   = new Paint(new ColorF(1f, 0.2f, 0.2f, 1f), default);
            var strokeW   = new Stroke(new ColorF(1f, 1f, 1f, 1f), 1f);
            
            var styleBlue = new DrawStyle(fillBlue, FeatherPx: 1.5f);
            var styleRed  = new DrawStyle(fillRed,  FeatherPx: 1.5f);

            // =====================================================
            // 1) Rotating transform test (rect + circle with stroke)
            // =====================================================
            using (layer.ScopeTranslate(win.Width / 2f, win.Height / 3f))
            {
                using (layer.ScopeRotate(time * 30f))
                {
                    var rectNode = new SdfRectNode
                    {
                        Rect = new RectBounds(-100, -50, 100, 50)
                    };
            
                    rectNode.DrawDebugRect(layer, DebugWhite(0.5f));
                    layer.DrawSdf(rectNode, styleBlue);
                }
            
                var circleStyle = new DrawStyle(
                    new Paint(new ColorF(0.8f, 0.8f, 0.2f, 1f), strokeW),
                    FeatherPx: 2f
                );
            
                var circleNode = new SdfCircleNode
                {
                    Center = new Vector2(0, 0),
                    Radius = 60
                };
            
                circleNode.DrawDebugRect(layer, DebugWhite(0.5f));
                layer.DrawSdf(circleNode, circleStyle);
            }

            // =========================
            // 2) Triangle (fill+stroke)
            // =========================
            var triStyle = new DrawStyle(
                new Paint(new ColorF(0.3f, 1f, 0.4f, 1f), strokeW),
                FeatherPx: 0f
            );
            
            var triNode = new SdfTriangleNode
            {
                A = new Vector2(50, 100),
                B = new Vector2(150, 50),
                C = new Vector2(200, 150)
            };
            
            triNode.DrawDebugRect(layer, DebugWhite(0.5f));
            layer.DrawSdf(triNode, triStyle);

            // ======================
            // 3) Animated polygon
            // ======================
            var polyPoints = new[]
            {
                new Vector2(0, -40),
                new Vector2(40, 0),
                new Vector2(0, 40),
                new Vector2(-40, 0)
            };
            
            using (layer.ScopeTranslate(700, 300))
            using (layer.ScopeRotate(time * 90f))
            {
                var polyNode = new SdfPolygonNode
                {
                    Points = polyPoints,
                };
            
                polyNode.DrawDebugRect(layer, DebugWhite(0.5f));
                layer.DrawSdf(polyNode, styleRed);
            }

            // ======================
            // 4) Line stroke test
            // ======================
            var strokeLine = new Stroke(new ColorF(1f, 1f, 1f, 1f), 1f);
            var lineStyle  = new DrawStyle(Paint.StrokeOnly(strokeLine), FeatherPx: 0f);
            
            var segNode = new SdfSegmentNode
            {
                P0 = new Vector2(50, 500),
                P1 = new Vector2(300, 550)
            };
            
            segNode.DrawDebugRect(layer, DebugWhite(0.5f));
            layer.DrawSdf(segNode, lineStyle);

            // ======================
            // 5) Nested transforms
            // ======================
            using (layer.ScopeTranslate(450, 400))
            {
                var barStyle = new DrawStyle(
                    new Paint(new ColorF(0.6f, 0.4f, 1f, 1f), default(Stroke)),
                    FeatherPx: 1.0f
                );

                var bars = new ISdf2Node[6];
                for (var i = 0; i < 6; i++)
                {
                    // local bar at origin
                    ISdf2Node bar = new SdfRectNode { Rect = new RectBounds(0, -10, 80, 10) };

                    // rotate around (0,0) in SDF space
                    var angle = time * 20f + i * 60f;
                    bar = Sdf.Rotate(bar, angle);

                    bars[i] = bar;
                }
                
                var union = Sdf.SmoothUnion(25, bars);

                union.DrawDebugRect(layer, DebugWhite(0.5f), barStyle);
                layer.DrawSdf(union, barStyle);
            }
            
            // ======================
            // 6) Subtract circle from rect
            // ======================
            using (layer.ScopeTranslate(500, 450))
            using (layer.ScopeRotate(15))
            {
                var subStyle = new DrawStyle(new Paint(new ColorF(0.4f, 1f, 0.4f, 1f), default(Stroke)), FeatherPx: 1.5f);
            
                var rectNode = Sdf.Rect(new RectBounds(-60, -60, 120, 120));
                var circleNode = Sdf.Circle(Vector2.Zero, 50);
                
                var subNode = new SdfSubtractNode
                {
                    A = rectNode,
                    Bs = [circleNode],
                };
                
                subNode.DrawDebugRect(layer, DebugWhite(0.5f));
                layer.DrawSdf(subNode, subStyle);
            }
            
            var hud = $"UPS: {ups:0.0}";
            var hudSize = layer.MeasureText(hud, styleHud);
            layer.DrawText(hud, 20, layer.Height - 20 - hudSize.Y, styleHud);

            layer.Render();
        };

        app.WaitForClose();
    }
}