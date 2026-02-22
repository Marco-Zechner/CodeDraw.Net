namespace MarcoZechner.MathDotNet;

public enum OriginLocation
{
    TopLeft, TopCenter, TopRight, 
    CenterLeft, Center, CenterRight, 
    BottomLeft, BottomCenter, BottomRight,
}

public static class OriginLocatingExtensions
{
    public static Origin ToOrigin(this OriginLocation origin)
    {
        return origin switch
        {
            OriginLocation.TopLeft => new Origin(0, 0),
            OriginLocation.TopCenter => new Origin(0.5f, 0),
            OriginLocation.TopRight => new Origin(1, 0),
            OriginLocation.CenterLeft => new Origin(0, 0.5f),
            OriginLocation.Center => new Origin(0.5f, 0.5f),
            OriginLocation.CenterRight => new Origin(1, 0.5f),
            OriginLocation.BottomLeft => new Origin(0, 1),
            OriginLocation.BottomCenter => new Origin(0.5f, 1),
            OriginLocation.BottomRight => new Origin(1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
        };
    }
}

public readonly record struct Origin(float X, float Y)
{
    public static implicit operator Origin((float x, float y) t) => new(t.x, t.y);
    public static implicit operator (double x, double y)(Origin o) => (o.X, o.Y);
    public static implicit operator (float x, float y)(Origin o) => (o.X, o.Y);
    
    public double Xd => X;
    public double Yd => Y;
    
    public void Deconstruct(out float x, out float y) { x = X; y = Y; }
}