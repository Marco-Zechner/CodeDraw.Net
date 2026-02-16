namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public readonly record struct RectF(float X, float Y, float W, float H)
{
    public float X2 => X + W;
    public float Y2 => Y + H;
    public bool IsEmpty => W <= 0 || H <= 0;

    public static RectF FromMinMax(float x1, float y1, float x2, float y2)
        => new(x1, y1, x2 - x1, y2 - y1);
}