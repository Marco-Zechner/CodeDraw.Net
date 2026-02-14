using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using MarcoZechner.ColorDotNet;
using Rgba = MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text.Rgba;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(5)]
public class Prototype5 : IDisposable
{
    [StaticPrototype]
    public static void RunTest()
    {
        var host = SharedGlfwHost.Instance;
        host.Start();

        using (new Prototype5(host))
        {
            host.WaitUntilAllWindowsClosed();
        }

        host.Stop();
    }
    
    public void Dispose()
    {
        foreach (var w in _windows)
        {
            w.Dispose();
        }
        
    }
    
    private readonly List<CodeDrawWindow> _windows = [];
    public Prototype5(SharedGlfwHost host)
    {
        var win = new CodeDrawWindow(host, 400, 400, 50, 50, "Prototype5");
        var layerSpin = CodeDrawShader.CsProject("layerSpin", "PrototypeTest/shaders");

        win.ResizeMode = WindowResizeMode.Aspect;
        
        win.OnUpdate += context =>
        {
            var layer = context.Win.Layer;
            layer.Clear(0,0,0,1);
            // layer.DrawRect(0,0, layer.Width, layer.Height, 0,0,0, 0.05f);
            
            TextStyle style = new TextStyle
            {
                Font = FontRef.FromFile("C:\\DevProjects\\CodeDraw.Net\\tests\\CodeDraw.Net.Tests.Manual\\resources\\fonts\\FiraCode-VF.ttf")
                    .WithVariant(FontVariant.BoldItalic),
                SizePx = 24,
                Align = TextAlign.Center,
                VAlign = TextVAlign.Middle,
            };
            
            // // Plain text
            // float time = layer.LayerAliveForSeconds();
            // //3 phase colors
            // float r = 0.5f + 0.5f * MathF.Sin(time * 2);
            // float g = 0.5f + 0.5f * MathF.Sin(time * 2 + 2);
            // float b = 0.5f + 0.5f * MathF.Sin(time * 2 + 4);
            // style.Color = new Rgba(r, g, b, 1);
            // layer.DrawText(">I<", x: 200, y: 200, style);
            // style.SizePx = 400;
            // style.Color = new Rgba(1, 1, 1, 1f);
            // layer.DrawText("O", x: 200, y: 200, style);
            // layer.DrawRect(200-1,200-1,2,2, 1,1,1,1); // baseline marker
            
            layer.DrawText("Hello\nWorld", x: 200, y: 200, style);   // multiline
            layer.DrawRect(200-25,200-(50-24)/2f,50,50-24, 1,1,1,0.5f); // text bounds
            
            // Rich text string (tags override base style)
            // layer.DrawRichText("[color=#ff0]Hello[/color] World", x: 20, y: 40, style);
            //
            // // Builder / precompiled
            // var specialText = TextBuilder.Create()
            //     .Text("my text")
            //     .WithColor(new Color(1, 0, 0, 1))
            //     .WithEffect(TextEffects.Wave(amplitudePx: 6, speed: 1.5f))
            //     .Append()
            //     .Text(" more")
            //     .WithColor(new Color(1, 1, 1, 1))
            //     .Append()
            //     .Build();

            // layer.DrawText(specialText, x: 20, y: 40, style); // base style applies where spans don't override
            
            // layer.PostProcess(layerSpin,
            //     uniforms: Uniforms.Of(
            //         UniformValue.Float("uTime", layer.LayerAliveForSeconds()),
            //         UniformValue.Float("uSpeed", 1.0f) // rad/s (≈ 57.3°/s). Use 2*PI for 1 rev/s
            //     ));
            
            layer.Render();
        };
    }
    
    // public sealed class WaveEffect : ITextEffect
    // {
    //     private readonly float _amplitudePx;
    //     private readonly float _speed;
    //
    //     public WaveEffect(float amplitudePx, float speed)
    //     {
    //         _amplitudePx = amplitudePx;
    //         _speed = speed;
    //     }
    //
    //     public void Apply(ref TextGlyphInstance inst, in TextGlyphContext ctx)
    //     {
    //         float t = ctx.TimeMs / 1000.0f;
    //         float phase = ctx.Index * 0.35f - t * _speed;
    //         float off = MathF.Sin(phase);
    //         inst.Y -= off * _amplitudePx;
    //     }
    // }
}