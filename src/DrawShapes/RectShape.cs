using MarcoZechner.Math;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet;

public record RectShape(Vector2 Position, Vector2 Size) : IDrawShape
{

    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawRect(Position.X, Position.Y, Size.X, Size.Y, paint);
    }
}