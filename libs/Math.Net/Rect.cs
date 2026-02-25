namespace MarcoZechner.MathDotNet;

public readonly record struct RectBounds(float Left, float Top, float Right, float Bottom)
{
    public static implicit operator Rect(RectBounds b) => new(new Vector2(b.Left, b.Top), new Vector2(b.Right - b.Left, b.Bottom - b.Top));
}

public readonly record struct RectWh(float X, float Y, float Width, float Height, OriginLocation Origin = OriginLocation.TopLeft)
{
    public static implicit operator Rect(RectWh r) => new(new Vector2(r.X, r.Y), new Vector2(r.Width, r.Height), r.Origin);
}

/// <summary>
/// 
/// </summary>
/// <param name="Position"></param>
/// <param name="Size"></param>
/// <param name="LocalOrigin">origin in rect-space, where (0,0)=top-left, (1,1)=bottom-right; values outside allowed</param>
public readonly record struct Rect(Vector2 Position, Vector2 Size, Origin LocalOrigin)
{
    public Rect(Vector2 position, Vector2 size, OriginLocation origin = OriginLocation.TopLeft) : this(position, size, origin.ToOrigin()) {}
    
    public Rect(float x, float y, float width, float height, OriginLocation origin) : this(new Vector2(x, y), new Vector2(width, height), origin) {}
    
    public Rect(float x, float y, float width, float height, float originX, float originY) : this(new Vector2(x, y), new Vector2(width, height), new Origin(originX, originY)) {}

    /// <summary>
    /// Create a rect from bounds (left, top, right, bottom), without checking for left &lt;= right or top &lt;= bottom.
    /// </summary>
    public Rect((float left, float top, float right, float bottom) bounds) : this(new Vector2(bounds.left, bounds.top), new Vector2(bounds.right - bounds.left, bounds.bottom - bounds.top)) {}
    
    /// <summary>
    /// Create a rect from position + size, with optional origin.
    /// </summary>
    public Rect((float x, float y) pos, (float width, float height) size, OriginLocation origin = OriginLocation.TopLeft) : this(new Vector2(pos.x, pos.y), new Vector2(size.width, size.height), origin) {}
    
#region Conversions
    
    public static explicit operator Rect(Rect<double> v) => new((Vector2)v.Position, (Vector2)v.Size, v.LocalOrigin);
    public static implicit operator Rect<double>(Rect v) => new(v.Position, v.Size, v.LocalOrigin);

    public static implicit operator Rect(Rect<float> v) => new(v.Position, v.Size, v.LocalOrigin);
    public static implicit operator Rect<float>(Rect v) => new(v.Position, v.Size, v.LocalOrigin);

    public static implicit operator Rect(Rect<int> v) => new(v.Position, v.Size, v.LocalOrigin);
    public static explicit operator Rect<int>(Rect v) => new((Vector2<int>)v.Position, (Vector2<int>)v.Size, v.LocalOrigin);

#endregion
    
    /// <summary>
    /// Create a rect from min/max corners, without checking for min &lt;= max.
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    public static Rect FromMinMaxUnchecked(Vector2 min, Vector2 max)
    {
        var size = max - min;
        return new Rect(min, size);
    }
    
    // -----------------------------
    // Core geometry (origin-aware)
    // -----------------------------

    /// <summary>Top-left corner in world space (origin-aware).</summary>
    public Vector2 TopLeft => Position - new Vector2(Size.X * LocalOrigin.X, Size.Y * LocalOrigin.Y);

    /// <summary>Bottom-right corner in world space (origin-aware).</summary>
    public Vector2 BottomRight => TopLeft + Size;

    public float Left => MathF.Min(TopLeft.X, BottomRight.X);
    public float Top => MathF.Min(TopLeft.Y, BottomRight.Y);
    public float Right => MathF.Max(TopLeft.X, BottomRight.X);
    public float Bottom => MathF.Max(TopLeft.Y, BottomRight.Y);

    public float Width => Right - Left;
    public float Height => Bottom - Top;
    
    public float X => Position.X;
    public float Y => Position.Y;
    
    public Vector2 Center => new((Left + Right) * 0.5f, (Top + Bottom) * 0.5f);

    public Vector2 Min => new(Left, Top);
    public Vector2 Max => new(Right, Bottom);

    public float Area => Width * Height;

    public bool IsEmpty => Width == 0f || Height == 0f;
    public bool IsDegenerate => Width <= 0f || Height <= 0f;

    /// <summary>Returns a rect with positive size and TopLeft origin, same covered area.</summary>
    public Rect NormalizedTopLeft()
    {
        var tl = new Vector2(Left, Top);
        var br = new Vector2(Right, Bottom);
        return FromMinMaxUnchecked(tl, br);
    }

    // -----------------------------
    // Corner + edge anchor points
    // -----------------------------
    public Vector2 TopCenter => new((Left + Right) * 0.5f, Top);
    public Vector2 BottomCenter => new((Left + Right) * 0.5f, Bottom);
    public Vector2 CenterLeft => new(Left, (Top + Bottom) * 0.5f);
    public Vector2 CenterRight => new(Right, (Top + Bottom) * 0.5f);

    public (Vector2 TL, Vector2 TR, Vector2 BR, Vector2 BL) Corners
        => (new Vector2(Left, Top), new Vector2(Right, Top), new Vector2(Right, Bottom), new Vector2(Left, Bottom));
    
    /// <summary>Get the point inside the rect at normalized coords (0..1, 0..1) using the rect's bounds.</summary>
    public Vector2 PointAt(Vector2 uv) => new(Left + Width * uv.X, Top + Height * uv.Y);
    
    // -----------------------------
    // Containment / intersection
    // -----------------------------

    public bool Contains(float x, float y, ContainsMode mode = ContainsMode.InclusiveMin) => Contains(new Vector2(x, y), mode);
    
    public bool Contains(Vector2 p, ContainsMode mode = ContainsMode.InclusiveMin)
    {
        var left   = (mode & ContainsMode.InclusiveLeft)   != 0 ? p.X >= Left   : p.X > Left;
        var right  = (mode & ContainsMode.InclusiveRight)  != 0 ? p.X <= Right  : p.X < Right;
        var top    = (mode & ContainsMode.InclusiveTop)    != 0 ? p.Y >= Top    : p.Y > Top;
        var bottom = (mode & ContainsMode.InclusiveBottom) != 0 ? p.Y <= Bottom : p.Y < Bottom;

        return left && right && top && bottom;
    }

    public bool Contains(Rect other, ContainsMode mode = ContainsMode.InclusiveMin)
    {
        var left   = (mode & ContainsMode.InclusiveLeft)   != 0 ? other.Left   >= Left   : other.Left   > Left;
        var right  = (mode & ContainsMode.InclusiveRight)  != 0 ? other.Right  <= Right  : other.Right  < Right;
        var top    = (mode & ContainsMode.InclusiveTop)    != 0 ? other.Top    >= Top    : other.Top    > Top;
        var bottom = (mode & ContainsMode.InclusiveBottom) != 0 ? other.Bottom <= Bottom : other.Bottom < Bottom;

        return left && right && top && bottom;
    }

    public bool Intersects(Rect other)
        => !(other.Right < Left || other.Left > Right || other.Bottom < Top || other.Top > Bottom);

    public Rect Intersection(Rect other)
    {
        var l = MathF.Max(Left, other.Left);
        var t = MathF.Max(Top, other.Top);
        var r = MathF.Min(Right, other.Right);
        var b = MathF.Min(Bottom, other.Bottom);

        if (r < l || b < t) return new Rect(new Vector2(l, t), new Vector2(0f, 0f));
        return new Rect(new Vector2(l, t), new Vector2(r - l, b - t));
    }

    public Rect Union(Rect other)
    {
        var l = MathF.Min(Left, other.Left);
        var t = MathF.Min(Top, other.Top);
        var r = MathF.Max(Right, other.Right);
        var b = MathF.Max(Bottom, other.Bottom);
        return new Rect(new Vector2(l, t), new Vector2(r - l, b - t));
    }
    
    /// <summary>Clamp a point into the rect bounds.</summary>
    public Vector2 ClampPoint(Vector2 p)
        => new(
            MathF.Min(MathF.Max(p.X, Left), Right),
            MathF.Min(MathF.Max(p.Y, Top), Bottom)
        );
    
    // -----------------------------
    // Manipulation
    // -----------------------------
    
    /// <summary>
    /// Scale existing rect to a new size, based around a given origin point, so that the new rect's position is adjusted to keep the origin point fixed in world space.
    /// </summary>
    /// <param name="newSize"></param>
    /// <param name="newOrigin"></param>
    /// <returns></returns>
    public Rect ResizedFrom(Vector2 newSize, OriginLocation newOrigin) => ResizedFrom(newSize, newOrigin.ToOrigin());
    
    /// <summary>
    /// Scale existing rect to a new size, based around a given origin point, so that the new rect's position is adjusted to keep the origin point fixed in world space.
    /// </summary>
    /// <param name="newSize"></param>
    /// <param name="newOrigin"></param>
    /// <returns></returns>
    public Rect ResizedFrom(Vector2 newSize, Origin? newOrigin = null)
    {
        var usedOrigin = newOrigin ?? LocalOrigin;
        var deltaOrigin = new Vector2(newSize.X * usedOrigin.X - Size.X * LocalOrigin.X, newSize.Y * usedOrigin.Y - Size.Y * LocalOrigin.Y);
        return new Rect(Position + deltaOrigin, newSize, usedOrigin);
    }
    
    public Rect OffsetEdges(float leftDelta, float topDelta, float rightDelta, float bottomDelta)
    {
        var newLeft = Left + leftDelta;
        var newTop = Top + topDelta;
        var newRight = Right + rightDelta;
        var newBottom = Bottom + bottomDelta;
        return FromMinMaxUnchecked(new Vector2(newLeft, newTop), new Vector2(newRight, newBottom));
    }
    
    public Rect Translated(Vector2 delta) => new(Position + delta, Size, LocalOrigin);
    
    public Rect ScaledFrom(Vector2 scale, OriginLocation newOrigin = OriginLocation.TopLeft) => ScaledFrom(scale, newOrigin.ToOrigin());
    
    public Rect ScaledFrom(Vector2 scale, Origin? newOrigin = null)
    {
        var usedOrigin = newOrigin ?? LocalOrigin;
        var newSize = new Vector2(Size.X * scale.X, Size.Y * scale.Y);
        var deltaOrigin = new Vector2(newSize.X * usedOrigin.X - Size.X * LocalOrigin.X, newSize.Y * usedOrigin.Y - Size.Y * LocalOrigin.Y);
        return new Rect(Position + deltaOrigin, newSize, usedOrigin);
    }
    
    public Rect LeftTo(float newLeft) => new(new Vector2(newLeft + Size.X * LocalOrigin.X, Position.Y), Size, LocalOrigin);
    public Rect TopTo(float newTop) => new(new Vector2(Position.X, newTop + Size.Y * LocalOrigin.Y), Size, LocalOrigin);
    public Rect RightTo(float newRight) => new(new Vector2(newRight - Size.X * (1f - LocalOrigin.X), Position.Y), Size, LocalOrigin);
    public Rect BottomTo(float newBottom) => new(new Vector2(Position.X, newBottom - Size.Y * (1f - LocalOrigin.Y)), Size, LocalOrigin);
    
    public Rect Expand(float delta) => OffsetEdges(-delta, -delta, delta, delta);
    
    // -----------------------------
    // Fit / aspect helpers (UI/game-cam friendly)
    // -----------------------------

    public Rect FitInside(Rect container, bool preserveAspect = true)
    {
        var srcW = Width;
        var srcH = Height;
        var dstW = container.Width;
        var dstH = container.Height;

        if (srcW <= 0f || srcH <= 0f || dstW <= 0f || dstH <= 0f)
            return new Rect(container.Center, new Vector2(0f, 0f), OriginLocation.Center);

        float scale;
        if (!preserveAspect) scale = 1f;
        else scale = MathF.Min(dstW / srcW, dstH / srcH);

        var newSize = new Vector2(srcW * scale, srcH * scale);
        var tl = container.Center - newSize * 0.5f;
        return FromMinMaxUnchecked(tl, tl + newSize);
    }

    public Rect FitOutside(Rect container, bool preserveAspect = true)
    {
        var srcW = Width;
        var srcH = Height;
        var dstW = container.Width;
        var dstH = container.Height;

        if (srcW <= 0f || srcH <= 0f || dstW <= 0f || dstH <= 0f)
            return new Rect(container.Center, new Vector2(0f, 0f), OriginLocation.Center);

        float scale;
        if (!preserveAspect) scale = 1f;
        else scale = MathF.Max(dstW / srcW, dstH / srcH);

        var newSize = new Vector2(srcW * scale, srcH * scale);
        var tl = container.Center - newSize * 0.5f;
        return FromMinMaxUnchecked(tl, tl + newSize);
    }
    
    private Rect BuildFromBounds(float l, float t, float r, float b, RectAnchorMode mode)
    {
        var size = new Vector2(r - l, b - t);
        var tl = new Vector2(l, t);

        // Degenerate: can't infer a meaningful origin from division
        if (size.X == 0f || size.Y == 0f)
        {
            return mode == RectAnchorMode.KeepPosition
                ? new Rect(Position, size, LocalOrigin)  // keep as-is
                : new Rect(tl + new Vector2(size.X * LocalOrigin.X, size.Y * LocalOrigin.Y), size, LocalOrigin);
        }

        if (mode == RectAnchorMode.KeepLocalOrigin)
        {
            // bounds are truth; keep LocalOrigin; recompute Position
            var pos = tl + new Vector2(size.X * LocalOrigin.X, size.Y * LocalOrigin.Y);
            return new Rect(pos, size, LocalOrigin);
        }

        // bounds are truth; keep Position; recompute LocalOrigin
        // Position = tl + size * origin  =>  origin = (Position - tl) / size
        var o = new Origin((Position.X - tl.X) / size.X, (Position.Y - tl.Y) / size.Y);
        return new Rect(Position, size, o);
    }

    public Rect ExpandedToInclude(Vector2 p, bool preserveAspect = true, RectAnchorMode anchor = RectAnchorMode.KeepLocalOrigin)
    {
        var l = MathF.Min(Left, p.X);
        var t = MathF.Min(Top, p.Y);
        var r = MathF.Max(Right, p.X);
        var b = MathF.Max(Bottom, p.Y);

        if (!preserveAspect || !(Width > 0f) || !(Height > 0f))
            return BuildFromBounds(l, t, r, b, anchor);

        var targetAspect = Width / Height;

        var w = r - l;
        var h = b - t;
        if (!(w > 0f) || !(h > 0f))
            return BuildFromBounds(l, t, r, b, anchor);

        // When preserving aspect, we must decide how to distribute "extra".
        // - KeepLocalOrigin: bias by LocalOrigin (stable in rect-space)
        // - KeepPosition: bias by where Position lies relative to the current bounds
        float ax, ay;
        if (anchor == RectAnchorMode.KeepLocalOrigin)
        {
            ax = LocalOrigin.X;
            ay = LocalOrigin.Y;
        }
        else
        {
            // anchor by fixed Position inside these candidate bounds
            ax = (Position.X - l) / w;
            ay = (Position.Y - t) / h;
            // no clamping: outside 0..1 is allowed and meaningful
        }

        var cur = w / h;

        if (cur < targetAspect)
        {
            // too narrow -> expand width
            var newW = h * targetAspect;
            var extra = newW - w;
            l -= extra * ax;
            r += extra * (1f - ax);
        }
        else if (cur > targetAspect)
        {
            // too wide -> expand height
            var newH = w / targetAspect;
            var extra = newH - h;
            t -= extra * ay;
            b += extra * (1f - ay);
        }

        return BuildFromBounds(l, t, r, b, anchor);
    }

    /// <summary>
    /// Shrinks the rect by the minimum amount needed to exclude the given point, if it is currently contained.
    /// If the point is outside, returns the original rect.
    /// If preserveAspect is true, keeps original aspect by shrinking the other axis if needed.
    /// </summary>
    public Rect ShrunkToExclude(Vector2 p, bool preserveAspect = true, RectAnchorMode anchor = RectAnchorMode.KeepLocalOrigin)
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

        switch (side)
        {
            case 0: l = MathF.BitIncrement(p.X); break;
            case 1: r = MathF.BitDecrement(p.X); break;
            case 2: t = MathF.BitIncrement(p.Y); break;
            case 3: b = MathF.BitDecrement(p.Y); break;
        }

        if (r <= l || b <= t)
            return new Rect(Position, new Vector2(0f, 0f), LocalOrigin);

        // If not preserving aspect, or if the rect is already degenerate, we can skip the more complex logic.
        if (!preserveAspect || !(Width > 0f) || !(Height > 0f)) return BuildFromBounds(l, t, r, b, anchor);

        var a = Width / Height;

        var w = r - l;
        var h = b - t;
        
        // Degenerate after moving edge? Can't infer a meaningful aspect from division, so skip correction.
        if (!(w > 0f) || !(h > 0f)) return BuildFromBounds(l, t, r, b, anchor);

        // distribution anchor:
        float ax, ay;
        if (anchor == RectAnchorMode.KeepLocalOrigin)
        {
            ax = LocalOrigin.X;
            ay = LocalOrigin.Y;
        }
        else
        {
            ax = (Position.X - l) / w;
            ay = (Position.Y - t) / h;
        }

        var cur = w / h;

        // Shrink only.
        if (cur > a)
        {
            var newW = h * a;
            var shrink = w - newW;
            l += shrink * ax;
            r -= shrink * (1f - ax);
        }
        else if (cur < a)
        {
            var newH = w / a;
            var shrink = h - newH;
            t += shrink * ay;
            b -= shrink * (1f - ay);
        }

        // After correction, it's possible that we overshot and made it degenerate. In that case, return a zero-size rect at the point.
        if (r <= l || b <= t)
            return new Rect(Position, new Vector2(0f, 0f), LocalOrigin);

        return BuildFromBounds(l, t, r, b, anchor);
    }
    
    // -----------------------------
    // Transform helpers (2D affine/projective)
    // -----------------------------

    public Rect TransformAffineAabb(Matrix3x3 m)
    {
        // Transform corners, then take AABB (axis-aligned bounding box)
        var tl = new Vector2(Left, Top);
        var tr = new Vector2(Right, Top);
        var br = new Vector2(Right, Bottom);
        var bl = new Vector2(Left, Bottom);

        var p0 = Matrix3x3.TransformAffine(m, tl);
        var p1 = Matrix3x3.TransformAffine(m, tr);
        var p2 = Matrix3x3.TransformAffine(m, br);
        var p3 = Matrix3x3.TransformAffine(m, bl);

        var minX = MathG.Min(MathG.Min(p0.X, p1.X), MathG.Min(p2.X, p3.X));
        var minY = MathG.Min(MathG.Min(p0.Y, p1.Y), MathG.Min(p2.Y, p3.Y));
        var maxX = MathG.Max(MathG.Max(p0.X, p1.X), MathG.Max(p2.X, p3.X));
        var maxY = MathG.Max(MathG.Max(p0.Y, p1.Y), MathG.Max(p2.Y, p3.Y));

        return FromMinMaxUnchecked(new Vector2(minX, minY), new Vector2(maxX, maxY));
    }
    
    public Rect TransformProjectiveAabb(Matrix3x3 m)
    {
        // Transform corners, then take AABB (axis-aligned bounding box)
        var tl = new Vector2(Left, Top);
        var tr = new Vector2(Right, Top);
        var br = new Vector2(Right, Bottom);
        var bl = new Vector2(Left, Bottom);

        var p0 = Matrix3x3.TransformProjective(m, tl);
        var p1 = Matrix3x3.TransformProjective(m, tr);
        var p2 = Matrix3x3.TransformProjective(m, br);
        var p3 = Matrix3x3.TransformProjective(m, bl);

        var minX = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
        var minY = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
        var maxX = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
        var maxY = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));

        return FromMinMaxUnchecked(new Vector2(minX, minY), new Vector2(maxX, maxY));
    }

    public Quad2 ToQuad() => new(Corners);

    public Quad2 TransformAffine(Matrix3x3 m)
    {
        var q = ToQuad();
        var p0 = Matrix3x3.TransformAffine(m, q.P0);
        var p1 = Matrix3x3.TransformAffine(m, q.P1);
        var p2 = Matrix3x3.TransformAffine(m, q.P2);
        var p3 = Matrix3x3.TransformAffine(m, q.P3);
        return new Quad2(p0, p1, p2, p3);
    }
    
    public Quad2 TransformProjective(Matrix3x3 m)
    {
        var q = ToQuad();
        var p0 = Matrix3x3.TransformProjective(m, q.P0);
        var p1 = Matrix3x3.TransformProjective(m, q.P1);
        var p2 = Matrix3x3.TransformProjective(m, q.P2);
        var p3 = Matrix3x3.TransformProjective(m, q.P3);
        return new Quad2(p0, p1, p2, p3);
    }
}