using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfSegmentNode : SdfNodeBase
{
    public Vector2 P0;
    public Vector2 P1;

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfSegment(P0, P1);
}