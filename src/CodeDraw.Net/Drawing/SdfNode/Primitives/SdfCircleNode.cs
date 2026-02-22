using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfCircleNode : SdfNodeBase
{
    public Vector2 Center;
    public float Radius;

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfCircle(Center, Radius);
}