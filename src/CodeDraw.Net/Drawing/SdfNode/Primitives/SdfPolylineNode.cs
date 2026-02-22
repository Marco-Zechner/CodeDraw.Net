using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfPolylineNode : SdfNodeBase
{
    public Vector2[] Points = [];
    public bool Closed = true;

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfPolyline(Points, Closed);
}