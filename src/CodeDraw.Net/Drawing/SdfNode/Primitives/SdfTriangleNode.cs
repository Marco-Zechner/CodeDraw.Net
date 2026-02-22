using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfTriangleNode : SdfNodeBase
{
    public Vector2 A;
    public Vector2 B;
    public Vector2 C;

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfTriangle(A, B, C);
}