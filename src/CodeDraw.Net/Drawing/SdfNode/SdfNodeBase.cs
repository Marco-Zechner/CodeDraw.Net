using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

/// <summary>
/// Optional convenience base so every node automatically gets:
/// - Distance
/// - CoverageBoundsPx
/// - DrawDebugRect
///
/// Inherit from this instead of manually re-implementing helpers.
/// </summary>
public abstract class SdfNodeBase : ISdf2Node
{
    ISdf2 ISdf2Node.Build(SdfCompileContext ctx) => Build(ctx);

    internal abstract ISdf2 Build(SdfCompileContext ctx);

    /// <summary>Distance query in layer/world space (same space you authored nodes in), with an optional transform.</summary>
    public float Distance(Vector2 pWorld, in Matrix3x3? worldToLocalOverride = null)
    {
        var sdf = SdfCompiler.Compile(this);
        if (worldToLocalOverride.HasValue)
        {
            var pLocal = Matrix3x3.TransformAffine(worldToLocalOverride.Value, pWorld);
            return sdf.DistanceLocal(pLocal);
        }

        // If no override, interpret authoring space as "local".
        return sdf.DistanceLocal(pWorld);
    }

    /// <summary>
    /// Bounds in LAYER PIXEL SPACE (world==layer) padded for AA feather and stroke.
    /// </summary>
    public Rect CoverageBoundsPx(in Matrix3x3 layerTransform, in DrawStyle style = default)
    {
        var compiled = SdfCompiler.Compile(this);
        var placed = new SdfPlaced(compiled, layerTransform);

        // Base world bounds of the compiled SDF (no feather/stroke yet)
        var bb = placed.WorldBounds;

        var feather = MathG.Max(0f, style.FeatherPx);
        var pad = feather;

        var stroke = style.Paint.Stroke;
        if (stroke is not { Thickness: > 0f, Color.A: > 0f })
            return bb.Expand(pad);

        var halfT = 0.5f * MathG.Max(0f, stroke.Thickness);

        // Safe pad (covers Inside/Outside/Center)
        pad = MathG.Max(pad, halfT + feather);

        return bb.Expand(pad);
    }

    /// <summary>
    /// Draws the padded coverage bounds as a debug rectangle in layer space.
    /// (Rect is computed with the caller's current transform, then drawn with identity.)
    /// </summary>
    public void DrawDebugRect(CodeDrawLayer layer, ColorF color, in DrawStyle style = default)
    {
        // Capture caller's transform BEFORE we mess with the stack.
        var xf = layer.CurrentTransform;

        // Compute bounds in LAYER space using the transform that was active at call site.
        var r = CoverageBoundsPx(xf, style);

        // Draw in layer space: ensure no extra transform is applied to the debug rect itself.
        using (layer.ScopePushTransform(Matrix3x3.Identity, TransformCombine.Replace))
        {
            layer.DrawDebugRect(r.Left, r.Top, r.Width, r.Height,
                color.R, color.G, color.B, color.A);
        }
    }
}