using System.Numerics;

namespace MarcoZechner.MathDotNet;

/// <summary>
/// Generic rect.
/// Position/Size are type T (e.g. int, float, double).
/// LocalOrigin is float-based (Origin) by design (0..1 typical, but outside allowed).
/// Rounding for integral T is AwayFromZero (via MathG.FromFloat/FromDouble).
/// </summary>
public readonly record struct Rect<T>(Vector2<T> Position, Vector2<T> Size, Origin LocalOrigin)
    where T : unmanaged, INumber<T>
{
    public Rect(Vector2<T> position, Vector2<T> size, OriginLocating origin = OriginLocating.TopLeft)
        : this(position, size, origin.ToOrigin()) { }

    public Rect(T x, T y, T width, T height, OriginLocating origin = OriginLocating.TopLeft)
        : this(new Vector2<T>(x, y), new Vector2<T>(width, height), origin.ToOrigin()) { }

    public Rect(T x, T y, T width, T height, Origin? origin)
        : this(new Vector2<T>(x, y), new Vector2<T>(width, height), origin ?? OriginLocating.TopLeft.ToOrigin()) { }

    #region Conversions

    public static implicit operator Rect<double>(Rect<T> v) => new(v.Position, v.Size, v.LocalOrigin);

    public static explicit operator Rect<float>(Rect<T> v) => new((Vector2<float>)v.Position, (Vector2<float>)v.Size, v.LocalOrigin);

    public static explicit operator Rect<int>(Rect<T> v) => new((Vector2<int>)v.Position, (Vector2<int>)v.Size, v.LocalOrigin);

    #endregion

    /// <summary>Create a rect from min/max corners, without checking for min &lt;= max.</summary>
    public static Rect<T> FromMinMaxUnchecked(Vector2<T> min, Vector2<T> max)
    {
        var size = max - min;
        return new Rect<T>(min, size, OriginLocating.TopLeft.ToOrigin());
    }

    // -----------------------------
    // Core geometry (origin-aware)
    // -----------------------------

    /// <summary>Top-left corner in world space (origin-aware).</summary>
    public Vector2<T> TopLeft
    {
        get
        {
            // TopLeft = Position - Size * LocalOrigin
            var px = MathG.ToFloat(Position.X);
            var py = MathG.ToFloat(Position.Y);
            var sx = MathG.ToFloat(Size.X);
            var sy = MathG.ToFloat(Size.Y);

            var tlx = px - sx * LocalOrigin.X;
            var tly = py - sy * LocalOrigin.Y;

            return new Vector2<T>(MathG.FromFloat<T>(tlx), MathG.FromFloat<T>(tly));
        }
    }

    /// <summary>Bottom-right corner in world space (origin-aware).</summary>
    public Vector2<T> BottomRight => TopLeft + Size;

    public T Left   => MathG.Min(TopLeft.X, BottomRight.X);
    public T Top    => MathG.Min(TopLeft.Y, BottomRight.Y);
    public T Right  => MathG.Max(TopLeft.X, BottomRight.X);
    public T Bottom => MathG.Max(TopLeft.Y, BottomRight.Y);

    public T Width  => Right - Left;
    public T Height => Bottom - Top;

    public Vector2<T> Center
        => new(
            MathG.FromFloat<T>((MathG.ToFloat(Left) + MathG.ToFloat(Right)) * 0.5f),
            MathG.FromFloat<T>((MathG.ToFloat(Top) + MathG.ToFloat(Bottom)) * 0.5f)
        );

    public Vector2<T> Min => new(Left, Top);
    public Vector2<T> Max => new(Right, Bottom);

    public T Area => Width * Height;

    public bool IsEmpty => MathG.IsZero(Width) || MathG.IsZero(Height);
    public bool IsDegenerate => Width <= T.Zero || Height <= T.Zero;

    /// <summary>Returns a rect with positive size and TopLeft origin, same covered area.</summary>
    public Rect<T> NormalizedTopLeft()
        => FromMinMaxUnchecked(new Vector2<T>(Left, Top), new Vector2<T>(Right, Bottom));

    // -----------------------------
    // Corner + edge anchor points
    // -----------------------------

    public Vector2<T> TopCenter
        => new(MathG.FromFloat<T>((MathG.ToFloat(Left) + MathG.ToFloat(Right)) * 0.5f), Top);

    public Vector2<T> BottomCenter
        => new(MathG.FromFloat<T>((MathG.ToFloat(Left) + MathG.ToFloat(Right)) * 0.5f), Bottom);

    public Vector2<T> CenterLeft
        => new(Left, MathG.FromFloat<T>((MathG.ToFloat(Top) + MathG.ToFloat(Bottom)) * 0.5f));

    public Vector2<T> CenterRight
        => new(Right, MathG.FromFloat<T>((MathG.ToFloat(Top) + MathG.ToFloat(Bottom)) * 0.5f));

    public (Vector2<T> TL, Vector2<T> TR, Vector2<T> BR, Vector2<T> BL) Corners
        => (new Vector2<T>(Left, Top),
            new Vector2<T>(Right, Top),
            new Vector2<T>(Right, Bottom),
            new Vector2<T>(Left, Bottom));

    /// <summary>Get the point inside the rect at normalized coords (0..1, 0..1) using the rect's bounds.</summary>
    public Vector2<T> PointAt(Vector2<T> uv)
    {
        // Left + Width*uv.X, Top + Height*uv.Y in float-space, then back to T
        var x = MathG.ToFloat(Left) + MathG.ToFloat(Width) * MathG.ToFloat(uv.X);
        var y = MathG.ToFloat(Top) + MathG.ToFloat(Height) * MathG.ToFloat(uv.Y);
        return new Vector2<T>(MathG.FromFloat<T>(x), MathG.FromFloat<T>(y));
    }

    // -----------------------------
    // Containment / intersection
    // -----------------------------

    public bool Contains(Vector2<T> p, ContainsMode mode = ContainsMode.InclusiveMin)
    {
        var left   = (mode & ContainsMode.InclusiveLeft)   != 0 ? p.X >= Left   : p.X > Left;
        var right  = (mode & ContainsMode.InclusiveRight)  != 0 ? p.X <= Right  : p.X < Right;
        var top    = (mode & ContainsMode.InclusiveTop)    != 0 ? p.Y >= Top    : p.Y > Top;
        var bottom = (mode & ContainsMode.InclusiveBottom) != 0 ? p.Y <= Bottom : p.Y < Bottom;

        return left && right && top && bottom;
    }

    public bool Contains(Rect<T> other, ContainsMode mode = ContainsMode.InclusiveMin)
    {
        var left   = (mode & ContainsMode.InclusiveLeft)   != 0 ? other.Left   >= Left   : other.Left   > Left;
        var right  = (mode & ContainsMode.InclusiveRight)  != 0 ? other.Right  <= Right  : other.Right  < Right;
        var top    = (mode & ContainsMode.InclusiveTop)    != 0 ? other.Top    >= Top    : other.Top    > Top;
        var bottom = (mode & ContainsMode.InclusiveBottom) != 0 ? other.Bottom <= Bottom : other.Bottom < Bottom;

        return left && right && top && bottom;
    }

    public bool Intersects(Rect<T> other)
        => !(other.Right < Left || other.Left > Right || other.Bottom < Top || other.Top > Bottom);

    public Rect<T> Intersection(Rect<T> other)
    {
        var l = MathG.Max(Left, other.Left);
        var t = MathG.Max(Top, other.Top);
        var r = MathG.Min(Right, other.Right);
        var b = MathG.Min(Bottom, other.Bottom);

        if (r < l || b < t)
            return new Rect<T>(new Vector2<T>(l, t), new Vector2<T>(T.Zero, T.Zero), OriginLocating.TopLeft);

        return FromMinMaxUnchecked(new Vector2<T>(l, t), new Vector2<T>(r, b));
    }

    public Rect<T> Union(Rect<T> other)
    {
        var l = MathG.Min(Left, other.Left);
        var t = MathG.Min(Top, other.Top);
        var r = MathG.Max(Right, other.Right);
        var b = MathG.Max(Bottom, other.Bottom);
        return FromMinMaxUnchecked(new Vector2<T>(l, t), new Vector2<T>(r, b));
    }

    /// <summary>Clamp a point into the rect bounds.</summary>
    public Vector2<T> ClampPoint(Vector2<T> p)
        => new(
            MathG.Clamp(p.X, Left, Right),
            MathG.Clamp(p.Y, Top, Bottom)
        );

    // -----------------------------
    // Manipulation
    // -----------------------------

    public Rect<T> ResizedFrom(Vector2<T> newSize, OriginLocating newOrigin) => ResizedFrom(newSize, newOrigin.ToOrigin());

    public Rect<T> ResizedFrom(Vector2<T> newSize, Origin? newOrigin = null)
    {
        var usedOrigin = newOrigin ?? LocalOrigin;

        var oldSx = MathG.ToFloat(Size.X);
        var oldSy = MathG.ToFloat(Size.Y);
        var newSx = MathG.ToFloat(newSize.X);
        var newSy = MathG.ToFloat(newSize.Y);

        var dx = newSx * usedOrigin.X - oldSx * LocalOrigin.X;
        var dy = newSy * usedOrigin.Y - oldSy * LocalOrigin.Y;

        var px = MathG.ToFloat(Position.X) + dx;
        var py = MathG.ToFloat(Position.Y) + dy;

        var newPos = new Vector2<T>(MathG.FromFloat<T>(px), MathG.FromFloat<T>(py));
        return new Rect<T>(newPos, newSize, usedOrigin);
    }

    public Rect<T> OffsetEdges(T leftDelta, T topDelta, T rightDelta, T bottomDelta)
    {
        var newLeft = Left + leftDelta;
        var newTop = Top + topDelta;
        var newRight = Right + rightDelta;
        var newBottom = Bottom + bottomDelta;
        return FromMinMaxUnchecked(new Vector2<T>(newLeft, newTop), new Vector2<T>(newRight, newBottom));
    }

    public Rect<T> Expand(T delta)
        => OffsetEdges(-delta, -delta, delta, delta);

    public Rect<T> Translated(Vector2<T> delta) => new(Position + delta, Size, LocalOrigin);

    public Rect<T> ScaledFrom(Vector2<T> scale, OriginLocating newOrigin = OriginLocating.TopLeft)
        => ScaledFrom(scale, newOrigin.ToOrigin());

    public Rect<T> ScaledFrom(Vector2<T> scale, Origin? newOrigin = null)
    {
        var usedOrigin = newOrigin ?? LocalOrigin;

        var newSize = new Vector2<T>(
            Size.X * scale.X,
            Size.Y * scale.Y
        );

        // adjust position so the chosen origin remains fixed in world space
        var oldSx = MathG.ToFloat(Size.X);
        var oldSy = MathG.ToFloat(Size.Y);
        var newSx = MathG.ToFloat(newSize.X);
        var newSy = MathG.ToFloat(newSize.Y);

        var dx = newSx * usedOrigin.X - oldSx * LocalOrigin.X;
        var dy = newSy * usedOrigin.Y - oldSy * LocalOrigin.Y;

        var px = MathG.ToFloat(Position.X) + dx;
        var py = MathG.ToFloat(Position.Y) + dy;

        var newPos = new Vector2<T>(MathG.FromFloat<T>(px), MathG.FromFloat<T>(py));
        return new Rect<T>(newPos, newSize, usedOrigin);
    }

    public Rect<T> LeftTo(T newLeft)
    {
        // Position.X = newLeft + Size.X * LocalOrigin.X
        var x = MathG.ToFloat(newLeft) + MathG.ToFloat(Size.X) * LocalOrigin.X;
        return new Rect<T>(new Vector2<T>(MathG.FromFloat<T>(x), Position.Y), Size, LocalOrigin);
    }

    public Rect<T> TopTo(T newTop)
    {
        var y = MathG.ToFloat(newTop) + MathG.ToFloat(Size.Y) * LocalOrigin.Y;
        return new Rect<T>(new Vector2<T>(Position.X, MathG.FromFloat<T>(y)), Size, LocalOrigin);
    }

    public Rect<T> RightTo(T newRight)
    {
        // right = position + size*(1-origin)
        var x = MathG.ToFloat(newRight) - MathG.ToFloat(Size.X) * (1f - LocalOrigin.X);
        return new Rect<T>(new Vector2<T>(MathG.FromFloat<T>(x), Position.Y), Size, LocalOrigin);
    }

    public Rect<T> BottomTo(T newBottom)
    {
        var y = MathG.ToFloat(newBottom) - MathG.ToFloat(Size.Y) * (1f - LocalOrigin.Y);
        return new Rect<T>(new Vector2<T>(Position.X, MathG.FromFloat<T>(y)), Size, LocalOrigin);
    }

    // -----------------------------
    // Fit / aspect helpers (UI/game-cam friendly)
    // (Generic version returns Rect<float> because aspect math is inherently float)
    // -----------------------------

    public Rect<float> FitInside(Rect<float> container, bool preserveAspect = true)
    {
        var srcW = MathG.ToFloat(Width);
        var srcH = MathG.ToFloat(Height);
        var dstW = container.Width;
        var dstH = container.Height;

        if (srcW <= 0f || srcH <= 0f || dstW <= 0f || dstH <= 0f)
            return new Rect<float>(container.Center, new Vector2<float>(0f, 0f), OriginLocating.Center);

        float scale = preserveAspect ? MathF.Min(dstW / srcW, dstH / srcH) : 1f;

        var newSize = new Vector2<float>(srcW * scale, srcH * scale);
        var tl = container.Center - newSize * 0.5f;
        return Rect<float>.FromMinMaxUnchecked((Vector2<float>)tl, (Vector2<float>)(tl + newSize));
    }

    public Rect<float> FitOutside(Rect<float> container, bool preserveAspect = true)
    {
        var srcW = MathG.ToFloat(Width);
        var srcH = MathG.ToFloat(Height);
        var dstW = container.Width;
        var dstH = container.Height;

        if (srcW <= 0f || srcH <= 0f || dstW <= 0f || dstH <= 0f)
            return new Rect<float>(container.Center, new Vector2<float>(0f, 0f), OriginLocating.Center);

        float scale = preserveAspect ? MathF.Max(dstW / srcW, dstH / srcH) : 1f;

        var newSize = new Vector2<float>(srcW * scale, srcH * scale);
        var tl = container.Center - newSize * 0.5f;
        return Rect<float>.FromMinMaxUnchecked((Vector2<float>)tl, (Vector2<float>)(tl + newSize));
    }

    // -----------------------------
    // Expand / shrink with anchor + aspect
    // -----------------------------

    private static bool IsIntegralT
    {
        get
        {
            var t = typeof(T);
            return t == typeof(sbyte) || t == typeof(byte) ||
                   t == typeof(short) || t == typeof(ushort) ||
                   t == typeof(int) || t == typeof(uint) ||
                   t == typeof(long) || t == typeof(ulong) ||
                   t == typeof(nint) || t == typeof(nuint) ||
                   t == typeof(BigInteger);
        }
    }

    private static T NextGreaterThan(T v)
    {
        if (IsIntegralT) return v + T.One;
        var vf = MathG.ToFloat(v);
        return MathG.FromFloat<T>(float.BitIncrement(vf));
    }

    private static T NextLessThan(T v)
    {
        if (IsIntegralT) return v - T.One;
        var vf = MathG.ToFloat(v);
        return MathG.FromFloat<T>(float.BitDecrement(vf));
    }

    private Rect<T> BuildFromBounds(T l, T t, T r, T b, RectAnchorMode mode)
    {
        var size = new Vector2<T>(r - l, b - t);
        var tl = new Vector2<T>(l, t);

        // Degenerate: division would be garbage for KeepPosition (origin inference).
        if (MathG.IsZero(size.X) || MathG.IsZero(size.Y))
        {
            if (mode == RectAnchorMode.KeepPosition)
                return new Rect<T>(Position, size, LocalOrigin);

            // KeepLocalOrigin: bounds are truth; keep LocalOrigin; recompute Position
            var tlx = MathG.ToFloat(tl.X);
            var tly = MathG.ToFloat(tl.Y);
            var sx = MathG.ToFloat(size.X);
            var sy = MathG.ToFloat(size.Y);

            var px = tlx + sx * LocalOrigin.X;
            var py = tly + sy * LocalOrigin.Y;

            return new Rect<T>(new Vector2<T>(MathG.FromFloat<T>(px), MathG.FromFloat<T>(py)), size, LocalOrigin);
        }

        if (mode == RectAnchorMode.KeepLocalOrigin)
        {
            // bounds are truth; keep LocalOrigin; recompute Position
            var tlx = MathG.ToFloat(tl.X);
            var tly = MathG.ToFloat(tl.Y);
            var sx = MathG.ToFloat(size.X);
            var sy = MathG.ToFloat(size.Y);

            var px = tlx + sx * LocalOrigin.X;
            var py = tly + sy * LocalOrigin.Y;

            return new Rect<T>(new Vector2<T>(MathG.FromFloat<T>(px), MathG.FromFloat<T>(py)), size, LocalOrigin);
        }

        // bounds are truth; keep Position; recompute LocalOrigin
        // Position = tl + size * origin  => origin = (Position - tl) / size
        var posx = MathG.ToFloat(Position.X);
        var posy = MathG.ToFloat(Position.Y);
        var tlxf = MathG.ToFloat(tl.X);
        var tlyf = MathG.ToFloat(tl.Y);
        var sxf  = MathG.ToFloat(size.X);
        var syf  = MathG.ToFloat(size.Y);

        var ox = (posx - tlxf) / sxf;
        var oy = (posy - tlyf) / syf;

        return new Rect<T>(Position, size, new Origin(ox, oy));
    }

    public Rect<T> ExpandedToInclude(Vector2<T> p, bool preserveAspect = true, RectAnchorMode anchor = RectAnchorMode.KeepLocalOrigin)
    {
        var l = MathG.Min(Left, p.X);
        var t = MathG.Min(Top, p.Y);
        var r = MathG.Max(Right, p.X);
        var b = MathG.Max(Bottom, p.Y);

        // If we don't preserve aspect, we're done.
        if (!preserveAspect) return BuildFromBounds(l, t, r, b, anchor);

        // Need a meaningful aspect ratio: use current rect in float space.
        var w0 = MathG.ToFloat(Width);
        var h0 = MathG.ToFloat(Height);
        if (!(w0 > 0f) || !(h0 > 0f)) return BuildFromBounds(l, t, r, b, anchor);

        var targetAspect = w0 / h0;

        var wf = MathG.ToFloat(r - l);
        var hf = MathG.ToFloat(b - t);
        if (!(wf > 0f) || !(hf > 0f)) return BuildFromBounds(l, t, r, b, anchor);

        // distribution anchor:
        float ax, ay;
        if (anchor == RectAnchorMode.KeepLocalOrigin)
        {
            ax = LocalOrigin.X;
            ay = LocalOrigin.Y;
        }
        else
        {
            // anchor by fixed Position inside these candidate bounds (no clamping, by your rules)
            var lf = MathG.ToFloat(l);
            var tf = MathG.ToFloat(t);
            var px = MathG.ToFloat(Position.X);
            var py = MathG.ToFloat(Position.Y);
            ax = (px - lf) / wf;
            ay = (py - tf) / hf;
        }

        var cur = wf / hf;

        if (cur < targetAspect)
        {
            // too narrow -> expand width
            var newW = hf * targetAspect;
            var extra = newW - wf;

            var lf = MathG.ToFloat(l) - extra * ax;
            var rf = MathG.ToFloat(r) + extra * (1f - ax);

            l = MathG.FromFloat<T>(lf);
            r = MathG.FromFloat<T>(rf);
        }
        else if (cur > targetAspect)
        {
            // too wide -> expand height
            var newH = wf / targetAspect;
            var extra = newH - hf;

            var tf = MathG.ToFloat(t) - extra * ay;
            var bf = MathG.ToFloat(b) + extra * (1f - ay);

            t = MathG.FromFloat<T>(tf);
            b = MathG.FromFloat<T>(bf);
        }

        return BuildFromBounds(l, t, r, b, anchor);
    }

    /// <summary>
    /// Shrinks the rect by the minimum amount needed to exclude the given point, if it is currently contained.
    /// If the point is outside, returns the original rect.
    /// If preserveAspect is true, keeps original aspect by shrinking the other axis if needed.
    /// </summary>
    public Rect<T> ShrunkToExclude(Vector2<T> p, bool preserveAspect = true, RectAnchorMode anchor = RectAnchorMode.KeepLocalOrigin)
    {
        if (!Contains(p)) return this;

        var l = Left;
        var t = Top;
        var r = Right;
        var b = Bottom;

        // Distances to edges (inside => all >= 0)
        var dxL = p.X - l;
        var dxR = r - p.X;
        var dyT = p.Y - t;
        var dyB = b - p.Y;

        // Nearest edge to move inward past p
        var best = dxL;
        var side = 0; // 0=Left,1=Right,2=Top,3=Bottom
        if (dxR < best) { best = dxR; side = 1; }
        if (dyT < best) { best = dyT; side = 2; }
        if (dyB < best) { best = dyB; side = 3; }

        // Move edge strictly past p (no longer inclusive-contained)
        switch (side)
        {
            case 0: l = NextGreaterThan(p.X); break; // Left > p.X
            case 1: r = NextLessThan(p.X);    break; // Right < p.X
            case 2: t = NextGreaterThan(p.Y); break; // Top > p.Y
            case 3: b = NextLessThan(p.Y);    break; // Bottom < p.Y
        }

        if (r <= l || b <= t)
            return new Rect<T>(Position, new Vector2<T>(T.Zero, T.Zero), LocalOrigin);

        if (!preserveAspect) return BuildFromBounds(l, t, r, b, anchor);

        // Desired aspect from original
        var w0 = MathG.ToFloat(Width);
        var h0 = MathG.ToFloat(Height);
        if (!(w0 > 0f) || !(h0 > 0f)) return BuildFromBounds(l, t, r, b, anchor);
        var a = w0 / h0;

        var wf = MathG.ToFloat(r - l);
        var hf = MathG.ToFloat(b - t);
        if (!(wf > 0f) || !(hf > 0f)) return BuildFromBounds(l, t, r, b, anchor);

        float ax, ay;
        if (anchor == RectAnchorMode.KeepLocalOrigin)
        {
            ax = LocalOrigin.X;
            ay = LocalOrigin.Y;
        }
        else
        {
            var lf = MathG.ToFloat(l);
            var tf = MathG.ToFloat(t);
            var px = MathG.ToFloat(Position.X);
            var py = MathG.ToFloat(Position.Y);
            ax = (px - lf) / wf;
            ay = (py - tf) / hf;
        }

        var cur = wf / hf;

        // Shrink only.
        if (cur > a)
        {
            // too wide -> shrink width to hf*a
            var newW = hf * a;
            var shrink = wf - newW;

            var lf = MathG.ToFloat(l) + shrink * ax;
            var rf = MathG.ToFloat(r) - shrink * (1f - ax);

            l = MathG.FromFloat<T>(lf);
            r = MathG.FromFloat<T>(rf);
        }
        else if (cur < a)
        {
            // too tall -> shrink height to wf/a
            var newH = wf / a;
            var shrink = hf - newH;

            var tf = MathG.ToFloat(t) + shrink * ay;
            var bf = MathG.ToFloat(b) - shrink * (1f - ay);

            t = MathG.FromFloat<T>(tf);
            b = MathG.FromFloat<T>(bf);
        }

        if (r <= l || b <= t)
            return new Rect<T>(Position, new Vector2<T>(T.Zero, T.Zero), LocalOrigin);

        return BuildFromBounds(l, t, r, b, anchor);
    }

    // -----------------------------
    // Transform helpers (2D affine/projective) -> typed results
    // -----------------------------

    public Quad2<T> ToQuad() => new(Corners);

    public Rect<T> TransformAffineAabb(Matrix3x3<T> m)
        => TransformAffine(m).Aabb;

    public Rect<T> TransformProjectiveAabb(Matrix3x3<T> m)
        => TransformProjective(m).Aabb;

    public Quad2<T> TransformAffine(Matrix3x3<T> m)
    {
        var q = ToQuad();

        var p0 = Matrix3x3.TransformAffine<T, T, T>(m, q.P0);
        var p1 = Matrix3x3.TransformAffine<T, T, T>(m, q.P1);
        var p2 = Matrix3x3.TransformAffine<T, T, T>(m, q.P2);
        var p3 = Matrix3x3.TransformAffine<T, T, T>(m, q.P3);

        return new Quad2<T>(p0, p1, p2, p3);
    }

    public Quad2<T> TransformProjective(Matrix3x3<T> m)
    {
        var q = ToQuad();

        var p0 = Matrix3x3.TransformProjective<T, T, T>(m, q.P0);
        var p1 = Matrix3x3.TransformProjective<T, T, T>(m, q.P1);
        var p2 = Matrix3x3.TransformProjective<T, T, T>(m, q.P2);
        var p3 = Matrix3x3.TransformProjective<T, T, T>(m, q.P3);

        return new Quad2<T>(p0, p1, p2, p3);
    }
}