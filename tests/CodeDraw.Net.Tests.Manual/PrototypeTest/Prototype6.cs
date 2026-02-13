using System.Diagnostics;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(6)]
public class Prototype6 : IDisposable
{
    private static SharedGlfwHost _host = null!;
    
    [StaticPrototype]
    public static void RunTest()
    {
        _host = SharedGlfwHost.Instance;
        _host.Start();

        using (new Prototype6())
        {
            _host.WaitUntilAllWindowsClosed();
        }

        _host.Stop();
        _host.Dispose();
    }
    
    public void Dispose()
    {
        foreach (var w in _windows)
        {
            w.Dispose();
        }
    }
    
    private readonly List<CodeDrawWindow> _windows = [];

    public Prototype6()
    {
        var window = new CodeDrawWindow(_host, 600,600,20,20, "Prototype6 - Window1");
        var postProcessingBloom = CodeDrawShader.CsProject("bloom", "PrototypeTest/shaders/ppShader");
        var layerSpin = CodeDrawShader.CsProject("layerSpin", "PrototypeTest/shaders");
        _windows.Add(window);
        

        window.OnUpdate += context =>
        {
            if (context.Input.GetKeyDown(Keys.A))
            {
                context.Win.ToggleResizeMode(WindowResizeMode.Aspect);
            }
            
            var layer = context.Win.Layer;
            layer.Clear(0,0,0,1);
            layer.DrawRect(100,100,100,100,1,1,0,1);

            var glow = 25 + 25 * MathF.Sin(layer.LayerAliveForSeconds() * 5f);
            
            layer.PostProcess(postProcessingBloom,
                uniforms: Uniforms.Of(
                    UniformValue.Float("uGlow", glow)
                )
            );
            
            layer.PostProcess(layerSpin,
                 uniforms: Uniforms.Of(
                     UniformValue.Float("uTime", layer.LayerAliveForSeconds()),
                     UniformValue.Float("uSpeed", 1.0f) // rad/s (≈ 57.3°/s). Use 2*PI for 1 rev/s
                 ));
                
            
            layer.Render();
        };
        

    }
}