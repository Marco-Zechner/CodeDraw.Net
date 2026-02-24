using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Demos;

public class HeightMapShadow
{
    [ConstructorPrototype("HeightMapShadow")]
    public HeightMapShadow()
    {
        using var app = CodeDrawHost.Start();

        var monitors = app.GetMonitors().ToArray();
        var monitor = monitors.Last();

        const int w = 1024;
        const int h = 1024;

        var window = new CodeDrawWindow(w, h, monitor.WorkX, monitor.WorkY, "Water Terrain");

        // (vertexName, fragmentName)
        var shHeight = CodeDrawShader.CsProject(("shader", "heightmap"), "Demos/shaders");
        var shShade  = CodeDrawShader.CsProject(("shader", "terrain_water"), "Demos/shaders");

        // Offscreen layer holding the packed heightmap
        var heightLayer = new CodeDrawLayer(window.Width, window.Height);
        heightLayer.OpenDebugWindow();

        // Parameters
        var mapPos = new Vector2(0f, -0.1f);
        var waterLevel = 0.30f;

        // Day color knobs
        var sunSetCol = new ColorF(0.50f, 0.10f, 0.10f);
        var midDayCol = new ColorF(1.00f, 1.00f, 0.90f);

        // FPS counter
        float fps = 0;
        int frames = 0;
        float fpsAccum = 0;

        window.OnUpdate += ctx =>
        {
            var dt = ctx.DeltaSeconds;

            frames++;
            fpsAccum += dt;
            if (fpsAccum >= 0.25f)
            {
                fps = frames / fpsAccum;
                frames = 0;
                fpsAccum = 0;
                window.Title = $"Water Terrain  |  FPS {fps:0.0}";
            }

            var time = window.Layer.TimeAliveSeconds;

            // Sun from mouse (0..1)
            var sun = new Vector2(
                (float)ctx.Input.MouseX / window.Width,
                1 - (float)ctx.Input.MouseY / window.Height
            );

            // Lower sun when further from center
            var dx = sun.X - 0.5f;
            var dy = sun.Y - 0.5f;
            var distSq = dx * dx + dy * dy;
            var sunD = MathG.Sqrt(distSq);
            var sunZ = MathG.Clamp(MathG.Sqrt(MathG.Max(0f, 1f - sunD * sunD)), 0f, 1f);

            var amb = 1f - distSq;
            var col = ColorF.Lerp(sunSetCol, midDayCol, 1f - distSq);

            RenderHeightmap(mapPos);
            RenderScene(time, col, amb, sun, sunZ);
        };

        app.WaitForClose();
        return;

        void RenderHeightmap(Vector2 mapPosLocal)
        {
            // PASS 1: heightmap bake -> heightLayer
            // (shader writes every pixel, so Clear() is optional, but keeps debugging sane)
            heightLayer.Clear(0, 0, 0, 1);

            heightLayer.DrawCustomRect(
                heightLayer.FullRect,
                shHeight,
                Uniforms.Of(
                    UniformValue.Float2("uMapPos", mapPosLocal),
                    UniformValue.Float("uHeightAmp", 1.0f)
                )
            );

            heightLayer.Render();
        }

        void RenderScene(float time, ColorF sunCol, float ambStrength, Vector2 sunPos, float sunZ)
        {
            // PASS 2: shade -> window
            var layer = window.Layer;
            layer.Clear(0, 0, 0, 1);

            layer.DrawCustomRect(
                layer.FullRect,
                shShade,
                Uniforms.Of(
                    UniformValue.Float("uTime", time),

                    UniformValue.Tex2D("uHeightMap", heightLayer),

                    UniformValue.Float("uWaterLevel", waterLevel),

                    UniformValue.Float3("uSunPos", sunPos.X, sunPos.Y, sunZ),

                    // pixel size (1/W, 1/H)
                    UniformValue.Float2("uPix", 1f / window.Width, 1f / window.Height),

                    UniformValue.Float("uAmbientStrength", ambStrength * 0.10f),
                    UniformValue.Float3("uAmbientColor", sunCol.R, sunCol.G, sunCol.B),
                    UniformValue.Float3("uLightColor",   sunCol.R, sunCol.G, sunCol.B)
                )
            );

            layer.Render();
        }
    }
}