using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

public interface ISdf2
{
    /// <summary>
    /// Signed distance in the shape's LOCAL space.
    /// Convention: d < 0 inside, d = 0 boundary, d > 0 outside.
    /// </summary>
    float DistanceLocal(Vector2 pLocal);

    /// <summary>
    /// Conservative local-space AABB (used for broad-phase culling).
    /// If unknown/expensive: return something big, but then you lose culling.
    /// </summary>
    Rect LocalBounds { get; }
}