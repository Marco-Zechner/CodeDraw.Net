using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;

public sealed class SdfSubtractNode : SdfNodeBase
{
    private ISdf2Node _a = null!;
    public required ISdf2Node A
    {
        get => _a;
        set { _a = value ?? throw new ArgumentNullException(nameof(value)); MarkDirty(); }
    }

    private ISdf2Node[] _bs = [];
    public required ISdf2Node[] Bs
    {
        get => _bs;
        set { _bs = value; MarkDirty(); }
    }

    override internal ISdf2 Build(SdfCompileContext ctx)
    {
        if (_a == null) throw new InvalidOperationException("SdfSubtractNode.A must not be null.");
        var a = SdfCompiler.Compile(_a, ctx);

        if (_bs.Length == 0) return a; // nothing to subtract

        var bs = new ISdf2[_bs.Length];
        for (var i = 0; i < _bs.Length; i++)
            bs[i] = SdfCompiler.Compile(_bs[i], ctx);

        return new SdfSubtractN(a, bs);
    }
}