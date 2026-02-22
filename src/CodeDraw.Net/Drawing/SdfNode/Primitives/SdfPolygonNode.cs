using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfPolygonNode : SdfNodeBase
{
    /// <summary>
    /// Mutable authoring array. Keep it non-null.
    /// </summary>
    private Vector2[] _points = [];
    public required Vector2[]? Points
    {
        get => _points;
        set { _points = value ?? []; MarkDirty(); }
    }

    override internal ISdf2 Build(SdfCompileContext ctx)
        => new SdfPolygon(_points); // assumes primitive takes ReadOnlySpan/array
}