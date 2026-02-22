using MarcoZechner.CodeDrawDotNet.Shaders;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;

internal sealed class CmdPostProcess : ICmd
{
    public CodeDrawShader? Shader;
    public Uniforms Uniforms;

    public void Exec(GL gl, CodeDrawLayer self)
    {
        var s = Shader;
        if (s is null) return;
        self.ExecPostProcess(gl, s, Uniforms);
    }
}