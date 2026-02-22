namespace MarcoZechner.MathDotNet;

public enum OriginLocating
{
    TopLeft, TopCenter, TopRight, 
    CenterLeft, Center, CenterRight, 
    BottomLeft, BottomCenter, BottomRight,
}

public static class OriginLocatingExtensions
{
    public static Origin ToOrigin(this OriginLocating origin)
    {
        return origin switch
        {
            OriginLocating.TopLeft => new Origin(0, 0),
            OriginLocating.TopCenter => new Origin(0.5f, 0),
            OriginLocating.TopRight => new Origin(1, 0),
            OriginLocating.CenterLeft => new Origin(0, 0.5f),
            OriginLocating.Center => new Origin(0.5f, 0.5f),
            OriginLocating.CenterRight => new Origin(1, 0.5f),
            OriginLocating.BottomLeft => new Origin(0, 1),
            OriginLocating.BottomCenter => new Origin(0.5f, 1),
            OriginLocating.BottomRight => new Origin(1, 1),
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