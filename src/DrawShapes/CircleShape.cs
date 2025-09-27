using MarcoZechner.Math;
using SkiaSharp;

namespace MarcoZechner.CodeDraw.Net;

public record CircleShape(Vector2 Position, float Radius) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawCircle(Position.X, Position.Y, Radius, paint);
    }
}