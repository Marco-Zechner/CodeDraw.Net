using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfPolylineNode : SdfNodeBase
{
    private Vector2[] _points = [];
    public required Vector2[]? Points
    {
        get => _points;
        set { _points = value ?? []; MarkDirty(); }
    }

    private bool _closed = true;
    public required bool Closed
    {
        get => _closed;
        set { _closed = value; MarkDirty(); }
    }
    
    private float _radius = 0f;
    public required float Radius
    {
        get => _radius;
        set { _radius = MathF.Max(0f, value); MarkDirty(); }
    }

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfPolyline(_points, _closed, _radius);
}