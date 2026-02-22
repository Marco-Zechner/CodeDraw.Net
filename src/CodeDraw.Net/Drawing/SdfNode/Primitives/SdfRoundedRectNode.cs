using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfRoundedRectNode : SdfNodeBase
{
    public Rect Rect;
    public float Radius;

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfRoundedRect(Rect, Radius);
}