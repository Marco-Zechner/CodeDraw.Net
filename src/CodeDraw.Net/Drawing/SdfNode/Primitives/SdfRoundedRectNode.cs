using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfRoundedRectNode : SdfNodeBase
{
    private Rect _rect;
    public required Rect Rect
    {
        get => _rect;
        set { _rect = value; MarkDirty(); }
    }

    private float _radius;
    public required float Radius
    {
        get => _radius;
        set { _radius = value; MarkDirty(); }
    }

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfRoundedRect(_rect, _radius);
}