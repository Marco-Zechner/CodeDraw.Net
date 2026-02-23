using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

/// <summary>
/// Node wrapper that tags its child with a material definition.
/// The GPU flattener will see the resulting <see cref="SdfMaterialTag"/> and
/// assign a MatId to every primitive emitted under this subtree.
/// </summary>
public sealed class SdfMaterialNode : SdfNodeBase
{
    private ISdf2Node? _child;
    public required ISdf2Node? Child
    {
        get => _child;
        set
        {
            if (ReferenceEquals(_child, value)) return;
            _child = value;
            MarkDirty();
        }
    }

    private SdfMaterialDef? _material;
    public required SdfMaterialDef? Material
    {
        get => _material;
        set
        {
            if (ReferenceEquals(_material, value)) return;
            _material = value;
            MarkDirty();
        }
    }

    internal override ISdf2 Build(SdfCompileContext ctx)
    {
        if (_child == null)
            throw new InvalidOperationException($"{nameof(SdfMaterialNode)} requires {nameof(Child)}.");

        var built = _child.Build(ctx);

        // If no material is specified, just pass-through.
        if (_material == null)
            return built;

        return new SdfMaterialTag(built, _material);
    }
}