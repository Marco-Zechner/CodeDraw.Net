using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;

internal readonly record struct CmdSdf(
    SdfPlaced Placed,
    DrawStyle Style,
    bool ForceStrokeOnly = false
) : ICmd
{
    public void Exec(GL gl, CodeDrawLayer self)
        => self.ExecSdf(gl, self, Placed, Style, ForceStrokeOnly);
}