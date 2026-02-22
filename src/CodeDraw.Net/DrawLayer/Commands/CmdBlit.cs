using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;

internal sealed class CmdBlit : ICmd
{
    public required CodeDrawLayer Src;

    public Rect SrcRectPx;
    public Rect DstRectPx;

    // Optional per-draw blend override
    public bool HasBlendOverride;
    public BlendMode BlendOverride;

    public void Exec(GL gl, CodeDrawLayer self)
        => self.ExecBlit(gl, self, SrcRectPx, DstRectPx, HasBlendOverride, BlendOverride);
}