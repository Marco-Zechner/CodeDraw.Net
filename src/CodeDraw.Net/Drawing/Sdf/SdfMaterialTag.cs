using MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

// This is the bridge: it carries a material definition for the flattener.
public sealed class SdfMaterialTag(ISdf2 child, SdfMaterialDef material) : ISdf2
{
    public ISdf2 Child { get; set; } = child;
    public SdfMaterialDef Material { get; set;  } = material;

    public Rect LocalBounds => Child.LocalBounds;
    public float DistanceLocal(Vector2 p) => Child.DistanceLocal(p);
}