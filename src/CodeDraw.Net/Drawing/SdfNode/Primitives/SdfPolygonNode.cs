using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfPolygonNode : SdfNodeBase
{
    /// <summary>
    /// Mutable authoring array. Keep it non-null.
    /// </summary>
    public Vector2[] Points = Array.Empty<Vector2>();

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfPolygon(Points); // assumes primitive takes ReadOnlySpan/array
}