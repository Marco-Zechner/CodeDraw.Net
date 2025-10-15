using MarcoZechner.Math;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet;

public record EllipseShape(Vector2 Center, Vector2 Size) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawOval(Center.X, Center.Y, Size.X, Size.Y, paint);
    }
}