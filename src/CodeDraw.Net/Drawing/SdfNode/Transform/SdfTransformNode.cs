using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Transform;

public sealed class SdfTransformNode : SdfNodeBase
{
    public required ISdf2Node Child;
    public Matrix3x3 LocalToParent = Matrix3x3.Identity;

    override internal ISdf2 Build(SdfCompileContext ctx)
    {
        if (Child == null) throw new InvalidOperationException("SdfTransformNode.Child must not be null.");
        var c = SdfCompiler.Compile(Child, ctx);
        return new SdfTransform(c, LocalToParent);
    }
}