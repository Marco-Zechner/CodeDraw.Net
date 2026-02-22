using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

public static class SdfMath
{
    public static float SmoothMin(float a, float b, float k)
    {
        // k > 0. Larger k => smoother blend.
        // Polynomial smooth-min (cheap + stable).
        var h = MathG.Clamp(0.5f + 0.5f * (b - a) / k, 0f, 1f);
        return MathG.Lerp(b, a, h) - k * h * (1f - h);
    }
}