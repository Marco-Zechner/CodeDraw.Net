using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

internal static class SdfPlacedFactory
{
    private sealed class SdfPrimitiveNode : SdfNodeBase
    {
        private ISdf2 _prim = default!;
        public required ISdf2 Prim
        {
            get => _prim;
            set { _prim = value ?? throw new ArgumentNullException(nameof(value)); MarkDirty(); }
        }

        override internal ISdf2 Build(SdfCompileContext ctx) => _prim;
    }

    public static SdfPlaced FromPrimitive(ISdf2 prim, in Matrix3x3 localToWorld, SdfMaterial? material = null)
    {
        var n = new SdfPrimitiveNode { Prim = prim,
            Material = material,
        };

        return new SdfPlaced(n, prim, localToWorld);
    }
}