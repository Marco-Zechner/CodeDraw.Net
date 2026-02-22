using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;

internal sealed class CmdCustomRect : ICmd
{
    public Rect<int> Rect; 
    public CodeDrawShader? Shader;
    public Uniforms Uniforms;

    public void Exec(GL gl, CodeDrawLayer self)
    {
        var s = Shader;
        if (s is null) return;

        self.ExecCustomRect(
            gl,
            Rect,
            s,
            Uniforms);
    }
}