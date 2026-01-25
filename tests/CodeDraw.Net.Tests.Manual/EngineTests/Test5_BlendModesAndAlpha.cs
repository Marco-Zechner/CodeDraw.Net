using MarcoZechner.CodeDrawDotNet.Api;
using MarcoZechner.CodeDrawDotNet.Api.Graphics.Enums;
using MarcoZechner.ColorDotNet;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.EngineTests;

[Order(5)]
public class Test5BlendModesAndAlpha : ITestable
{
    public void RunTest()
    {
        var win = new CodeDrawWindow("Test5_BlendModes_AlphaMask")
        {
            Size = new(900, 500),
            Resizable = true,
            VSync = true,
            UpdateIntervalMs = 16,
            ClearColor = new Color(0.08f, 0.1f, 0.13f, 1.0f),
        };

        float t = 0;

        win.Update += (w, dt) =>
        {
            t += (float)dt;

            // You said: "we don't clear (or do but it doesn't matter)".
            // For readability, keep a single clear on first frame only:
            // (comment this out if you want true accumulation)
            w.Clear(w.ClearColor);

            // Split into 4 quads (window space)
            int width = w.Size.X;
            int height = w.Size.Y;
            float hw = width * 0.5f;
            float hh = height * 0.5f;

            // ─────────────────────────────────────────────────────────────
            // A) BlendMode: REPLACE
            // 4 quads: blue opaque / blue transparent / red opaque / red transparent
            // ─────────────────────────────────────────────────────────────
            w.SetBlendMode2D(BlendMode2D.OPAQUE_REPLACE);

            // TL: blue opaque
            w.FillRect(0, 0, hw, hh, new Color(0.15f, 0.35f, 0.95f, 1.0f));

            // TR: blue transparent (note: replace mode means it overwrites, including alpha)
            w.FillRect(hw, 0, hw, hh, new Color(0.15f, 0.35f, 0.95f, 0.5f));

            // BL: red opaque
            w.FillRect(0, hh, hw, hh, new Color(0.95f, 0.20f, 0.20f, 1.0f));

            // BR: red transparent
            w.FillRect(hw, hh, hw, hh, new Color(0.95f, 0.20f, 0.20f, 0.5f));

            // ─────────────────────────────────────────────────────────────
            // B) BlendMode: BLEND (RGB blends, DST alpha preserved)
            // 1 green circle in center
            // ─────────────────────────────────────────────────────────────
            w.SetBlendMode2D(BlendMode2D.RGB_BLEND_KEEP_DST_ALPHA);

            float cx = hw;
            float cy = hh;
            float rGreen = MathF.Min(hw, hh) * 0.35f;

            // NOTE: This needs FillCircle(...) implemented.
            // If you don't have it yet, replace with a bunch of triangles or a rect for now.
            w.FillCircle(cx, cy, rGreen, new Color(0.15f, 0.90f, 0.25f, 0.65f));

            // ─────────────────────────────────────────────────────────────
            // C) BlendMode: ONLY ALPHA (WriteAlphaReplace)
            // smaller circle, sinus alpha, changes the alpha of inner region of the green circle
            // ─────────────────────────────────────────────────────────────
            w.SetBlendMode2D(BlendMode2D.WRITE_ALPHA_REPLACE);

            float inner = rGreen * 0.45f;
            float a = 0.5f + 0.5f * MathF.Sin(t * 2.0f); // 0..1
            // RGB ignored due to ColorMask in WRITE_ALPHA_REPLACE
            w.FillCircle(cx, cy, inner, new Color(0, 0, 0, a));

            // ─────────────────────────────────────────────────────────────
            // D) BlendMode: BLEND again
            // yellow circle moving in a square path through all 4 quads
            // ─────────────────────────────────────────────────────────────

            // Square path: move along a loop inside the full window
            float pad = MathF.Min(hw, hh) * 0.15f;
            float x0 = pad;
            float y0 = pad;
            float x1 = width - pad;
            float y1 = height - pad;

            // Param 0..4 for 4 edges
            float speed = 0.25f; // loops per second-ish
            float p = (t * speed) % 4.0f;

            float px, py;
            if (p < 1.0f)
            {
                float u = p;
                px = Lerp(x0, x1, u); py = y0;
            }
            else if (p < 2.0f)
            {
                float u = p - 1.0f;
                px = x1; py = Lerp(y0, y1, u);
            }
            else if (p < 3.0f)
            {
                float u = p - 2.0f;
                px = Lerp(x1, x0, u); py = y1;
            }
            else
            {
                float u = p - 3.0f;
                px = x0; py = Lerp(y1, y0, u);
            }

            float rYellow = MathF.Min(hw, hh) * 0.10f;
            w.SetBlendMode2D(BlendMode2D.RGB_BLEND_KEEP_DST_ALPHA);
            w.FillCircle(px, py, rYellow, new Color(0.98f, 0.92f, 0.20f, 0.75f));

            // ─────────────────────────────────────────────────────────────
            // E) BlendMode: SourceOverAlpha
            // yellow circle moving in a square path through all 4 quads
            // ─────────────────────────────────────────────────────────────
            w.SetBlendMode2D(BlendMode2D.RGB_BLEND_SOURCEOVER_ALPHA);
            w.FillCircle(width-px, height-py, rYellow, new Color(0.98f, 0.92f, 0.20f, 0.75f));

            w.Show();
        };

        win.Key += (k, sc, a, m) =>
        {
            if (k == Keys.Escape && a == InputAction.Press)
                win.Close();
        };

        win.Open();

        Console.WriteLine(
            "Expected:\n" +
            "- 4 colored quadrants (replace mode): blue/blue(alpha)/red/red(alpha)\n" +
            "- a translucent green circle centered\n" +
            "- inner region alpha pulses (write-alpha)\n" +
            "- yellow circle moves in a square path across all quads\n" +
            "ESC closes."
        );

        win.WaitForClose();
        Console.WriteLine("Closed. Press ENTER to exit…");
        Console.ReadLine();
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
