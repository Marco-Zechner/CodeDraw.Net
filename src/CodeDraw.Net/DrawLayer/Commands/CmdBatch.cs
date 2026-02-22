using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;

internal readonly record struct CmdBatch(ICmd[] Commands) : ICmd
{
    public void Exec(GL gl, CodeDrawLayer self)
    {
        foreach (var c in Commands)
            c.Exec(gl, self);
    }
}