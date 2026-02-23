using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    internal static void ExecSdf(GL gl, CodeDrawLayer self, SdfPlaced placed, DrawStyle style, bool forceStrokeOnly)
    {
        // ExecSdfCpu(gl, self, placed, style, forceStrokeOnly);
        self.ExecSdfGpu(gl, placed, style, forceStrokeOnly);
    }
}