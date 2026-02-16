using MarcoZechner.MathDotNet;
using SkiaSharp;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.DrawShapes;

public record CircleShape(Vector2 Position, float Radius) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawCircle(Position.X, Position.Y, Radius, paint);
    }
}