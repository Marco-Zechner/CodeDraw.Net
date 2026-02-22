using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;

public sealed class SdfTriangleNode : SdfNodeBase
{
    private Vector2 _a;
    public required Vector2 A
    {
        get => _a;
        set { _a = value; MarkDirty(); }
    }

    private Vector2 _b;
    public required Vector2 B
    {
        get => _b;
        set { _b = value; MarkDirty(); }
    }

    private Vector2 _c;
    public required Vector2 C
    {
        get => _c;
        set { _c = value; MarkDirty(); }
    }

    override internal ISdf2 Build(SdfCompileContext ctx) => new SdfTriangle(_a, _b, _c);
}