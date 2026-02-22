using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfSegmentNode : SdfNodeBase
{
    private Vector2 _p0;
    public required Vector2 P0
    {
        get => _p0;
        set { _p0 = value; MarkDirty(); }
    }

    private Vector2 _p1;
    public required Vector2 P1
    {
        get => _p1;
        set { _p1 = value; MarkDirty(); }
    }

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfSegment(_p0, _p1);
}