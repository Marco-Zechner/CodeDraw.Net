using MarcoZechner.Math;
using SkiaSharp;

namespace MarcoZechner.CodeDraw.Net;

public record PointShape(Vector2 Position) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawPoint(Position.X, Position.Y, paint);
    }
}