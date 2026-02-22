namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

public static class SdfCoverage
{
    /// <summary>
    /// Convert signed distance to alpha using a feather region (anti-alias).
    /// feather = ~1px in world units (or screen units, depending where you apply it).
    /// </summary>
    public static float FillAlpha(float signedDistance, float feather)
    {
        if (feather <= 0f)
            return signedDistance < 0f ? 1f : 0f;
        // inside => 1, outside => 0
        // smooth around boundary in [-feather, +feather]
        var t = (signedDistance + feather) / (2f * feather);
        t = MathF.Min(MathF.Max(t, 0f), 1f);
        // invert because signedDistance<0 should be opaque
        return 1f - t;
    }

    /// <summary>
    /// Stroke alpha around boundary with thickness (centered stroke).
    /// </summary>
    public static float StrokeAlpha(float signedDistance, float halfThickness, float feather)
    {
        // distance to the stroke band = |d| - halfThickness
        var band = MathF.Abs(signedDistance) - halfThickness;
        return FillAlpha(band, feather);
    }
}