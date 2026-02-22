using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;

internal sealed class CmdRect : ICmd
{
    public float X, Y, W, H;
    public float R, G, B, A;
    public void Exec(GL gl, CodeDrawLayer self) => self.ExecRect(gl, X, Y, W, H, R, G, B, A);
}