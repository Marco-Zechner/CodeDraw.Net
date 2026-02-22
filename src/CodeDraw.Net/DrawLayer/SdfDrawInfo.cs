using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public readonly record struct SdfDrawInfo(
    SdfPlaced Placed,
    DrawStyle Style,
    bool ForceStrokeOnly
)
{
    public Rect WorldBounds => Placed.WorldBounds;
    public Rect LocalBounds => Placed.Shape.LocalBounds;
    public Matrix3x3 LocalToWorld => Placed.LocalToWorld;

    public bool TryGetWorldToLocal(out Matrix3x3 w2L)
        => Placed.TryGetWorldToLocal(out w2L);

    /// <summary>
    /// Conservative bounds for coverage in layer pixel space.
    /// This is the rectangle your CPU rasterizer *should* touch, and what you should draw as debug rect.
    /// </summary>
    public Rect EstimatedCoverageBoundsPx
    {
        get
        {
            var feather = MathF.Max(0f, Style.FeatherPx);

            var stroke = Style.Paint.Stroke;
            var halfStroke = stroke is { Thickness: > 0f, Color.A: > 0f } ? stroke.Thickness * 0.5f : 0f;

            // If you want stroke-only, stroke dominates. If fill+stroke, take the max pad.
            var pad = ForceStrokeOnly ? halfStroke + feather : MathF.Max(halfStroke, 0f) + feather;

            // +2 as a cheap safety margin because you floor/ceil later.
            return WorldBounds.Expand(pad + 2f);
        }
    }
    
    public void DrawDebugRect(CodeDrawLayer layer, ColorF color)
    {
        var bb = EstimatedCoverageBoundsPx;
        layer.DrawDebugRect(bb.Left, bb.Top, bb.Width, bb.Height, color.R, color.G, color.B, color.A);
    }
}