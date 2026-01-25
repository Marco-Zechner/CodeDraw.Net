using System.Diagnostics;
using MarcoZechner.CodeDrawDotNet.Api;
using MarcoZechner.CodeDrawDotNet.Interfaces;
using MarcoZechner.ColorDotNet;
using MarcoZechner.CodeDrawDotNet.Api.Graphics.Enums;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.EngineTests;

[Order(6)]
public class Test6SharedCanvasMirror : ITestable
{
    public void RunTest()
    {
        var winA = new CodeDrawWindow("Test6_A_Source")
        {
            Size = new(800, 450),
            Resizable = true,
            VSync = true,
            UpdateIntervalMs = 16,
            ClearColor = new Color(0.08f, 0.1f, 0.13f, 1.0f),
        };

        var winB = new CodeDrawWindow("Test6_B_Mirror")
        {
            Size = new(800, 450),
            Resizable = true,
            VSync = true,
            UpdateIntervalMs = 16,
            ClearColor = new Color(0.08f, 0.1f, 0.13f, 1.0f),
        };

        ILayerHandle? aCanvas = null;

        float t = 0;

        // A draws a moving scene into its own canvas
        winA.Update += (w, dt) =>
        {
            t += (float)dt;

            // Keep it deterministic / obvious
            w.Clear(w.ClearColor);

            float width = w.Size.X;
            float height = w.Size.Y;

            // background quads (opaque replace)
            w.SetBlendMode2D(BlendMode2D.OPAQUE_REPLACE);
            w.FillRect(0,    0,    width * 0.5f, height * 0.5f, new Color(0.18f, 0.22f, 0.55f, 1f));
            w.FillRect(width*0.5f,0,   width * 0.5f, height * 0.5f, new Color(0.55f, 0.18f, 0.22f, 1f));
            w.FillRect(0,    height*0.5f,width * 0.5f, height * 0.5f, new Color(0.20f, 0.55f, 0.25f, 1f));
            w.FillRect(width*0.5f,height*0.5f,width * 0.5f, height * 0.5f, new Color(0.55f, 0.50f, 0.18f, 1f));

            // animated circles (source-over alpha)
            w.SetBlendMode2D(BlendMode2D.RGB_BLEND_SOURCEOVER_ALPHA);

            var cx = width * 0.5f + MathF.Sin(t * 1.3f) * (width * 0.22f);
            var cy = height * 0.5f + MathF.Cos(t * 1.1f) * (height * 0.18f);

            w.FillCircle(cx, cy, MathF.Min(width, height) * 0.16f, new Color(0.98f, 0.92f, 0.20f, 0.75f));
            w.FillCircle(width * 0.5f, height * 0.5f, MathF.Min(width, height) * 0.10f, new Color(0.20f, 0.95f, 0.55f, 0.65f));

            var sw = Stopwatch.StartNew();
            w.Show();
            sw.Stop();

            if (sw.ElapsedMilliseconds > 5)
                Console.WriteLine($"[Update] WindowA Show() blocked for {sw.ElapsedMilliseconds} ms (Backlog={w.BacklogFrames}, Inflight={w.InflightFrames}, Queue={w.QueuedFrames})");

            // IMPORTANT: grab the stable handle AFTER at least one frame
            aCanvas ??= winA.CanvasLayer; // your new opaque ILayerHandle property
        };

        // B mirrors A's canvas by drawing A's canvas-layer into B's canvas
        winB.Update += (w, _) =>
        {
            if (aCanvas is null)
            {
                w.Clear(w.ClearColor);
                w.SetBlendMode2D(BlendMode2D.OPAQUE_REPLACE);
                w.FillRect(20, 20, 420, 70, new Color(0.9f, 0.2f, 0.2f, 1f));
                w.Show();
                return;
            }

            // Mirror: copy A's canvas into B (no premultiply here; we're drawing into B's canvas texture)
            w.SetBlendMode2D(BlendMode2D.OPAQUE_REPLACE);
            w.DrawLayer(aCanvas, premultiply: false);

            // Add a little “B overlay” so we know this is not the same window
            w.SetBlendMode2D(BlendMode2D.RGB_BLEND_SOURCEOVER_ALPHA);
            w.FillRect(20, 20, 220, 55, new Color(0f, 0f, 0f, 0.35f));
            w.FillRect(25, 25, 210, 45, new Color(0.2f, 0.8f, 0.3f, 0.35f));

            var sw = Stopwatch.StartNew();
            w.Show();
            sw.Stop();

            if (sw.ElapsedMilliseconds > 5)
                Console.WriteLine($"[Update] WindowB Show() blocked for {sw.ElapsedMilliseconds} ms (Backlog={w.BacklogFrames}, Inflight={w.InflightFrames}, Queue={w.QueuedFrames})");
        };

        void CloseBoth()
        {
            try { winA.Close(); }
            catch
            {
                // ignored
            }

            try { winB.Close(); }
            catch
            {
                // ignored
            }
        }

        winA.Key += (k, _, a, _) => { if (k == Keys.Escape && a == InputAction.Press) CloseBoth(); };
        winB.Key += (k, _, a, _) => { if (k == Keys.Escape && a == InputAction.Press) CloseBoth(); };

        winA.Open();
        winB.Open();

        Console.WriteLine("Expected: Window A shows animated scene. Window B mirrors A (same animation) + has its own small overlay. ESC closes both.");
        winA.WaitForClose();
        winB.WaitForClose();

        Console.WriteLine("Closed. Press ENTER to exit…");
        Console.ReadLine();
    }
}
