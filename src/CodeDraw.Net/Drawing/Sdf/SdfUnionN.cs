using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

internal readonly record struct SdfUnionN(ISdf2[] Children) : ISdf2
{
    public float DistanceLocal(Vector2 p) 
        => Children.Aggregate(float.PositiveInfinity, (current, c) => MathF.Min(current, c.DistanceLocal(p)));

    public Rect LocalBounds
    {
        get
        {
            if (Children.Length == 0) return default;
            var b = Children[0].LocalBounds;
            for (var i = 1; i < Children.Length; i++)
                b = b.Union(Children[i].LocalBounds);
            return b;
        }
    }
}