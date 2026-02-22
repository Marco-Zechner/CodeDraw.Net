using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;
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
        using var app = CodeDrawHost.Started();

        var win = new CodeDrawWindow(900, 600, 50, 50, "SDF Prototype 1");
        var time = 0f;

        // ---- shared debug styling ----
        static ColorF DebugWhite(float a = 0.5f) => ((ColorF)Colors.WHITE) with { A = a };

        win.OnUpdate += ctx =>
        {
            var layer = ctx.Win.Layer;
            time += ctx.DeltaSeconds;

            layer.Clear(0.1f, 0.1f, 0.12f, 1f);

            // ---- basic styles ----
            var fillBlue  = new Paint(new ColorF(0.2f, 0.5f, 1f, 1f), default);
            var fillRed   = new Paint(new ColorF(1f, 0.2f, 0.2f, 1f), default);
            var strokeW   = new Stroke(new ColorF(1f, 1f, 1f, 1f), 1f);

            var styleBlue = new DrawStyle(fillBlue, FeatherPx: 1.5f);
            var styleRed  = new DrawStyle(fillRed,  FeatherPx: 1.5f);

            var centerRect = new Rect(win.Width/2f, win.Height/2f, 100, 100, OriginLocating.Center);
            using (layer.ScopeRotateAround(centerRect.Position.X, centerRect.Position.Y, 45))
                layer.DrawDebugRect(centerRect.Left, centerRect.Top, centerRect.Width, centerRect.Height, 1,1,1,0.5f);
            
            // =====================================================
            // 1) Rotating transform test (rect + circle with stroke)
            // =====================================================
            using (layer.ScopeTranslate(win.Width / 2f, win.Height / 3f))
            {
                using (layer.ScopeRotate(time * 30f))
                {
                    var rectNode = new SdfRectNode
                    {
                        Rect = new Rect(-100, -50, 100, 50)
                    };

                    layer.DrawSdf(rectNode, styleBlue);
                    rectNode.DrawDebugRect(layer, DebugWhite(0.5f));
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

                layer.DrawSdf(circleNode, circleStyle);
                circleNode.DrawDebugRect(layer, DebugWhite(0.5f));
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

            layer.DrawSdf(triNode, triStyle);
            triNode.DrawDebugRect(layer, DebugWhite(0.5f));

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
                    Points = polyPoints
                };

                layer.DrawSdf(polyNode, styleRed);
                polyNode.DrawDebugRect(layer, DebugWhite(0.5f));
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

            layer.DrawSdf(segNode, lineStyle);
            segNode.DrawDebugRect(layer, DebugWhite(0.5f));

            // ======================
            // 5) Nested transforms
            // ======================
            using (layer.ScopeTranslate(450, 400))
            {
                var barStyle = new DrawStyle(
                    new Paint(new ColorF(0.6f, 0.4f, 1f, 1f), default(Stroke)),
                    FeatherPx: 1.0f
                );

                for (var i = 0; i < 6; i++)
                {
                    using (layer.ScopeRotate(time * 20f + i * 60f))
                    {
                        var barNode = new SdfRectNode
                        {
                            Rect = new Rect(0, -10, 80, 10)
                        };

                        layer.DrawSdf(barNode, barStyle);
                        barNode.DrawDebugRect(layer, DebugWhite(0.5f));
                    }
                }
            }

            layer.Render();
        };

        app.WaitForClose();
    }
}