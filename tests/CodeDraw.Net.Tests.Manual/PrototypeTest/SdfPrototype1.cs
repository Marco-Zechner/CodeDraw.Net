using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.DrawLayer;
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

        win.OnUpdate += ctx =>
        {
            var layer = ctx.Win.Layer;

            time += ctx.DeltaSeconds;

            layer.Clear(0.1f, 0.1f, 0.12f, 1f);

            // layer.Rect(
            //     new Rect(0, 0, layer.Width, layer.Height),
            //     new DrawStyle(Paint.FillOnly(new ColorF(0.1f, 0.1f, 0.12f, 1f)))
            // );
            
            // ---- basic styles ----

            var fillBlue = new Paint(new ColorF(0.2f, 0.5f, 1f, 1f), default);
            var fillRed = new Paint(new ColorF(1f, 0.2f, 0.2f, 1f), default);

            var strokeWhite = new Stroke(new ColorF(1, 1, 1, 1), 3f);

            var styleBlue = new DrawStyle(fillBlue, FeatherPx: 1.5f);
            var styleRed = new DrawStyle(fillRed, FeatherPx: 1.5f);

            // ---- rotating transform test ----

            using (layer.TranslateScope(win.Width / 2f, win.Height / 3f))
            {
                using (layer.RotateScopeDeg(time * 30f))
                {
                    layer.Rect(
                        new Rect(-100, -50, 100, 50),
                        styleBlue
                    ).DrawDebugRect(layer, ((ColorF)Colors.WHITE) with { A = 0.5f });
                }
                
                layer.Circle(
                    new Vector2(0, 0),
                    60,
                    new DrawStyle(
                        new Paint(
                            new ColorF(0.8f, 0.8f, 0.2f, 1f),
                            strokeWhite
                        ),
                        FeatherPx: 2f
                    )
                ).DrawDebugRect(layer, ((ColorF)Colors.WHITE) with { A = 0.5f });
            }

            // ---- triangle ----

            var triStyle = new DrawStyle(
                new Paint(new ColorF(0.3f, 1f, 0.4f, 1f), strokeWhite),
                FeatherPx: 1.2f
            );

            layer.Triangle(
                new Vector2(50, 100),
                new Vector2(150, 50),
                new Vector2(200, 150),
                triStyle
            ).DrawDebugRect(layer, ((ColorF)Colors.WHITE) with { A = 0.5f });
            
            // ---- animated polygon ----

            Span<Vector2> poly =
            [
                new(0, -40),
                new(40, 0),
                new(0, 40),
                new(-40, 0)
            ];

            using (layer.TranslateScope(700, 300))
            using (layer.RotateScopeDeg(time * 90f))
            {
                layer.Polygon(
                    poly, styleRed
                ).DrawDebugRect(layer, ((ColorF)Colors.WHITE) with { A = 0.5f });
            }

            // ---- line stroke test ----

            var stroke = new Stroke(new ColorF(1, 1, 1, 1), 5f);

            layer.Line(
                new Vector2(50, 500),
                new Vector2(300, 550),
                stroke
            ).DrawDebugRect(layer, ((ColorF)Colors.WHITE) with { A = 0.5f });

            // ---- nested transforms ----

            using (layer.TranslateScope(450, 400))
            {
                for (var i = 0; i < 6; i++)
                {
                    using (layer.RotateScopeDeg(time * 20f + i * 60))
                    {
                        layer.Rect(
                            new Rect(0, -10, 80, 10),
                            new DrawStyle(
                                new Paint(new ColorF(0.6f, 0.4f, 1f, 1f), default(Stroke))
                            )
                        ).DrawDebugRect(layer, ((ColorF)Colors.WHITE) with { A = 0.5f });
                    }
                }
            }

            layer.Render();
        };

        app.WaitForClose();
    }
}