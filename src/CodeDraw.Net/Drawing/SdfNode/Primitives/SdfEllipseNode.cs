using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfEllipseNode : SdfNodeBase
{
    public Vector2 Center;
    public Vector2 Radius;

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfEllipse(Center, Radius);
}