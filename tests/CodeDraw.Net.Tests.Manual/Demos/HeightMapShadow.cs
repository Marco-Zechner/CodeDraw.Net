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

            ComputeSky(
                sunZ,
                out var sunCol,
                out var ambCol,
                out var ambStrength,
                out var lightIntensity
            );

            RenderHeightmap(mapPos);
            RenderScene(time, sunCol, ambCol, ambStrength, sun, sunZ, lightIntensity);
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
                UniformValue.Float2("uMapPos", mapPosLocal),
                UniformValue.Float("uHeightAmp", 10.35f),
                UniformValue.Float("uIslandRadius", 0.0001f),
                UniformValue.Float("uIslandEdge", 0.8f),
                UniformValue.Float("uPeakSoftness", 0.65f),
                UniformValue.Float("uDetail", 0.15f),
                UniformValue.Float("uSeaLevelBias", -0.30f)
            );

            heightLayer.Render();
        }

        void RenderScene(float time, ColorF sunCol, ColorF ambCol, float ambStrength, Vector2 sunPos, float sunZ, float lightIntensity)
        {
            var layer = window.Layer;
            layer.Clear(0, 0, 0, 1);

            layer.DrawCustomRect(
                layer.FullRect,
                shShade,
                UniformValue.Float("uTime", time),

                UniformValue.Tex2D("uHeightMap", heightLayer),
                UniformValue.Float("uWaterLevel", waterLevel),

                UniformValue.Float3("uSunPos", sunPos.X, sunPos.Y, sunZ),
                UniformValue.Float2("uPix", 1f / window.Width, 1f / window.Height),

                UniformValue.Float("uAmbientStrength", ambStrength),
                UniformValue.Float3("uAmbientColor", ambCol.R, ambCol.G, ambCol.B),

                // IMPORTANT: scale light color by intensity (your shader multiplies lightColor)
                UniformValue.Float3("uLightColor", sunCol.R * lightIntensity, sunCol.G * lightIntensity, sunCol.B * lightIntensity),

                UniformValue.Float("uTerrainSpec", 0.04f),
                UniformValue.Float("uWaterSpec", 0.45f),
                UniformValue.Float("uTerrainRough", .95f)
            );

            layer.Render();
        }
    }
    
    static float Clamp01(float x) => x < 0 ? 0 : (x > 1 ? 1 : x);

    static float Smooth01(float x)
    {
        x = Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    static ColorF Lerp(ColorF a, ColorF b, float t) => ColorF.Lerp(a, b, Clamp01(t));

    static ColorF Mul(ColorF c, float s) => new ColorF(c.R * s, c.G * s, c.B * s, c.A);

    static ColorF Pow(ColorF c, float p)
    {
        // clamp to avoid NaNs if anything goes negative
        float r = MathF.Pow(MathF.Max(c.R, 0f), p);
        float g = MathF.Pow(MathF.Max(c.G, 0f), p);
        float b = MathF.Pow(MathF.Max(c.B, 0f), p);
        return new ColorF(r, g, b, c.A);
    }

    // Main: derive plausible colors from sun elevation
    static void ComputeSky(
        float sunZ,
        out ColorF sunColor,
        out ColorF skyAmbientColor,
        out float ambientStrength,
        out float lightIntensity)
    {
        sunZ = Clamp01(sunZ);

        // "dayness": pushes transition to happen closer to horizon (more sunset time)
        float day = Smooth01(MathF.Pow(sunZ, 0.55f)); // 0 at horizon, 1 near zenith

        // Sun color: warm at horizon -> white-ish at zenith
        var sunWarm = new ColorF(1.00f, 0.42f, 0.14f, 1f); // sunset/orange
        var sunNoon = new ColorF(1.00f, 0.98f, 0.92f, 1f); // slightly warm white
        sunColor = Lerp(sunWarm, sunNoon, day);

        // Sun intensity: very low near horizon, bright midday
        lightIntensity = MathG.Lerp(0.15f, 1.25f, day);

        // Sky ambient color: deep blue at zenith, warmer/gray near horizon
        var skyHorizon = new ColorF(0.35f, 0.30f, 0.28f, 1f); // dusk-ish / desaturated
        var skyZenith  = new ColorF(0.22f, 0.40f, 0.65f, 1f); // blue sky light
        skyAmbientColor = Lerp(skyHorizon, skyZenith, day);

        // Ambient strength: weaker at sunset, stronger midday
        ambientStrength = MathG.Lerp(0.08f, 0.35f, day);

        // Optional: tame extremes (prevents nuclear whites)
        sunColor = Pow(sunColor, 1.0f);          // keep as-is (tweak to 1.1 if too saturated)
        skyAmbientColor = Pow(skyAmbientColor, 1.0f);
    }
}