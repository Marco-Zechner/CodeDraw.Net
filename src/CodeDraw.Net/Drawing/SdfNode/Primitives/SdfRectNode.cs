using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfRectNode : SdfNodeBase
{
    public Rect Rect;

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfRect(Rect);
}