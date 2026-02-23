using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;

internal readonly record struct CmdSdf(
    SdfPlaced Placed,
    DrawStyle Style,
    bool ForceStrokeOnly = false,
    SdfDrawAreaOverride? DrawAreaOverride = null, 
    int MaxBlendSdfs = 8
) : ICmd
{
    public void Exec(GL gl, CodeDrawLayer self)
        => CodeDrawLayer.ExecSdf(gl, self, Placed, Style, ForceStrokeOnly, DrawAreaOverride, MaxBlendSdfs);
}