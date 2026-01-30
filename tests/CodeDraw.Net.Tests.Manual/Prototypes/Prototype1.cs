namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

[Prototype(1)]
public class Prototype1 : ITestable
{
    public void RunTest()
    {
        var host = SharedGlfwHost.Instance;
        host.Start();

        var winA = new CodeDrawWindow(host, 800, 450, "A");
        var winB = new CodeDrawWindow(host, 800, 450, "B (mirrors A)");

        // B presents A's layer (read-only presenter)
        winB.Layer = winA.Layer;

        // NEW: overlay is its own layer (produced by tB)
        var overlay = new CodeDrawLayer(host, 800, 450);

        var invert = new CodeDrawLayer.CodeDrawShader(
            host,
            GlShader.LayerShader.VS,
            """
            #version 330 core
            in vec2 vUV;
            out vec4 FragColor;
            uniform sampler2D uTex;
            void main(){
                vec4 c = texture(uTex, vUV);
                FragColor = vec4(1.0 - c.rgb, c.a);
            }
            """
        );

        var tA = new Thread(() =>
        {
            float t = 0f;
            while (!winA.ShouldClose)
            {
                t += 0.016f;

                winA.Layer!.EnsureCanvas(800, 450);

                // Frame ownership: only tA defines frames on winA.Layer
                winA.Layer.Clear(0.10f, 0.11f, 0.13f, 1f);

                // base content
                winA.Layer.DrawRect(60 + 120 * MathF.Sin(t), 80, 220, 140, 0.2f, 1.0f, 0.6f, 1f);
                winA.Layer.DrawRect(90, 260, 140, 80, 1.0f, 0.3f, 0.2f, 0.9f);


                // compose overlay (produced elsewhere)
                winA.Layer.SetBlendMode(CodeDrawLayer.BlendMode.BLEND_RBG_SOURCEOVER_ALPHA);
                winA.Layer.SetLayerBlitShader(invert);
                winA.Layer.DrawLayer(overlay);
                winA.Layer.SetLayerBlitShader(null); // back to default
                winA.Layer.SetBlendMode(CodeDrawLayer.BlendMode.ALPHA);

                // present frame
                winA.Layer.Render();

                Thread.Sleep(16);
            }
        })
        { IsBackground = true, Name = "Update-A" };

        var tB = new Thread(() =>
        {
            float t = 0f;
            while (!winB.ShouldClose)
            {
                t += 0.033f;

                // overlay produces its own frames; no touching winA.Layer
                overlay.EnsureCanvas(800, 450);
                overlay.Clear();
                overlay.SetBlendMode(CodeDrawLayer.BlendMode.BLEND_RBG_SOURCEOVER_ALPHA);
                overlay.DrawRect(10, 10, 260, 40, 0.2f, 0.4f, 1.0f, 0.5f + 0.5f * MathF.Sin(t * 2f));
                overlay.SetBlendMode(CodeDrawLayer.BlendMode.ALPHA);
                overlay.Render();

                Thread.Sleep(33);
            }
        })
        { IsBackground = true, Name = "Update-Overlay" };

        tA.Start();
        tB.Start();

        Console.WriteLine("Prototype1 running. Press ENTER to stop.");
        Console.ReadLine();

        winA.Close();
        winB.Close();

        tA.Join();
        tB.Join();

        // dispose layers explicitly
        overlay.Dispose();
        winA.Layer?.Dispose();

        winA.WaitForClose();
        winB.WaitForClose();

        host.Stop();
    }
}
