using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Transform;

public sealed class SdfTransformNode : SdfNodeBase
{
    private ISdf2Node _child = null!;
    public ISdf2Node Child
    {
        get => _child;
        set { _child = value ?? throw new ArgumentNullException(nameof(value)); MarkDirty(); }
    }

    private Matrix3x3 _localToParent = Matrix3x3.Identity;
    public Matrix3x3 LocalToParent
    {
        get => _localToParent;
        set { _localToParent = value; MarkDirty(); }
    }

    override internal ISdf2 Build(SdfCompileContext ctx)
    {
        var c = SdfCompiler.Compile(Child, ctx);
        return new SdfTransform(c, LocalToParent);
    }
}