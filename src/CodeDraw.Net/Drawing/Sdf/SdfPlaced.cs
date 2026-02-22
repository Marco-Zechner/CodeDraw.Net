using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

public readonly record struct SdfPlaced(ISdf2 Shape, Matrix3x3 LocalToWorld)
{
    public readonly ISdf2 Shape = Shape;

    // Cached inverse is worth it if you evaluate many points/pixels.
    // If you don't have a place to store it yet, compute lazily outside.
    public bool TryGetWorldToLocal(out Matrix3x3 worldToLocal)
        => Matrix3x3.TryInvert(LocalToWorld, out worldToLocal);

    public float DistanceWorld(Vector2 pWorld)
    {
        if (!TryGetWorldToLocal(out var w2L))
            throw new InvalidOperationException("SDF transform not invertible.");

        var pLocal = Matrix3x3.TransformAffine(w2L, pWorld);
        return Shape.DistanceLocal(pLocal);
    }

    /// <summary>Conservative world AABB of the local bounds (affine AABB of transformed corners).</summary>
    public Rect WorldBounds
    {
        get
        {
            // Uses your existing Rect.TransformAffineAabb(Matrix3x3)
            return Shape.LocalBounds.TransformAffineAabb(LocalToWorld);
        }
    }
}