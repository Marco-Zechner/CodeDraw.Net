using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

internal readonly record struct SdfUnionN(ISdf2[] Children) : ISdf2
{
    public float DistanceLocal(Vector2 p)
    {
        if (Children.Length == 0) return float.PositiveInfinity;
        var d = Children[0].DistanceLocal(p);
        for (var i = 1; i < Children.Length; i++)
            d = MathF.Min(d, Children[i].DistanceLocal(p));
        return d;
    }

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

internal readonly record struct SdfIntersectN(ISdf2[] Children) : ISdf2
{
    public float DistanceLocal(Vector2 p)
    {
        if (Children.Length == 0) return float.PositiveInfinity;
        var d = Children[0].DistanceLocal(p);
        for (var i = 1; i < Children.Length; i++)
            d = MathF.Max(d, Children[i].DistanceLocal(p));
        return d;
    }

    public Rect LocalBounds
    {
        get
        {
            if (Children.Length == 0) return default;
            var b = Children[0].LocalBounds;
            for (var i = 1; i < Children.Length; i++)
                b = b.Intersection(Children[i].LocalBounds);
            return b;
        }
    }
}

/// <summary>
/// A - (B0 union B1 union ...).
/// </summary>
internal readonly record struct SdfSubtractN(ISdf2 A, ISdf2[] Bs) : ISdf2
{
    public float DistanceLocal(Vector2 p)
    {
        var da = A.DistanceLocal(p);
        if (Bs.Length == 0) return da;

        var db = Bs[0].DistanceLocal(p);
        for (var i = 1; i < Bs.Length; i++)
            db = MathF.Min(db, Bs[i].DistanceLocal(p));

        return MathF.Max(da, -db);
    }

    public Rect LocalBounds => A.LocalBounds; // conservative
}

internal readonly record struct SdfSmoothUnionN(ISdf2[] Children, float K) : ISdf2
{
    public float DistanceLocal(Vector2 p)
    {
        if (Children.Length == 0) return float.PositiveInfinity;

        // k<=0 means "no smoothing"
        var k = MathF.Max(0f, K);
        var d = Children[0].DistanceLocal(p);

        if (k <= 0f)
        {
            for (var i = 1; i < Children.Length; i++)
                d = MathF.Min(d, Children[i].DistanceLocal(p));
            return d;
        }

        for (var i = 1; i < Children.Length; i++)
            d = SdfMath.SmoothMin(d, Children[i].DistanceLocal(p), k);

        return d;
    }

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

/// <summary>
/// Smooth intersection using smooth-max: smoothMax(a,b,k) = -smoothMin(-a,-b,k).
/// </summary>
internal readonly record struct SdfSmoothIntersectN(ISdf2[] Children, float K) : ISdf2
{
    private static float SmoothMax(float a, float b, float k) => -SdfMath.SmoothMin(-a, -b, k);

    public float DistanceLocal(Vector2 p)
    {
        if (Children.Length == 0) return float.PositiveInfinity;

        var k = MathF.Max(0f, K);
        var d = Children[0].DistanceLocal(p);

        if (k <= 0f)
        {
            for (var i = 1; i < Children.Length; i++)
                d = MathF.Max(d, Children[i].DistanceLocal(p));
            return d;
        }

        for (var i = 1; i < Children.Length; i++)
            d = SmoothMax(d, Children[i].DistanceLocal(p), k);

        return d;
    }

    public Rect LocalBounds
    {
        get
        {
            if (Children.Length == 0) return default;
            var b = Children[0].LocalBounds;
            for (var i = 1; i < Children.Length; i++)
                b = b.Intersection(Children[i].LocalBounds);
            return b;
        }
    }
}

/// <summary>
/// Smooth subtraction: A - union(Bs) with smooth-max between A and -B.
/// smoothMax(da, -db, k) = -smoothMin(-da, db, k)
/// </summary>
internal readonly record struct SdfSmoothSubtractN(ISdf2 A, ISdf2[] Bs, float K) : ISdf2
{
    private static float SmoothMax(float a, float b, float k) => -SdfMath.SmoothMin(-a, -b, k);

    public float DistanceLocal(Vector2 p)
    {
        var da = A.DistanceLocal(p);
        if (Bs.Length == 0) return da;

        var k = MathF.Max(0f, K);

        // db = union(Bs) as distance
        float db;
        if (k <= 0f)
        {
            db = Bs[0].DistanceLocal(p);
            for (var i = 1; i < Bs.Length; i++)
                db = MathF.Min(db, Bs[i].DistanceLocal(p));
        }
        else
        {
            db = Bs[0].DistanceLocal(p);
            for (var i = 1; i < Bs.Length; i++)
                db = SdfMath.SmoothMin(db, Bs[i].DistanceLocal(p), k);
        }

        if (k <= 0f) return MathF.Max(da, -db);

        // smooth(A, -B)
        return SmoothMax(da, -db, k);
    }

    public Rect LocalBounds => A.LocalBounds; // conservative
}