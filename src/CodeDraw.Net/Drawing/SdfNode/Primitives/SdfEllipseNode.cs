using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfEllipseNode : SdfNodeBase
{
    private Vector2 _center;
    public required Vector2 Center
    {
        get => _center;
        set { _center = value; MarkDirty(); }
    }

    private Vector2 _radius;
    public required Vector2 Radius
    {
        get => _radius;
        set { _radius = value; MarkDirty(); }
    }

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfEllipse(_center, _radius);
}