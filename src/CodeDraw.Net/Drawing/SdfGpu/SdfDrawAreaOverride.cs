using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;

public readonly record struct SdfDrawAreaOverride(
    Rect<int> RectPx,
    SdfDrawAreaMode Mode = SdfDrawAreaMode.Replace // Replace or Expand
);

public enum SdfDrawAreaMode
{
    Replace = 0, // use RectPx as the draw quad area
    Expand  = 1, // union(normalTightBounds, RectPx)
}