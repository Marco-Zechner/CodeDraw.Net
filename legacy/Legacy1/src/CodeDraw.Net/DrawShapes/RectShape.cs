using MarcoZechner.MathDotNet;
using SkiaSharp;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.DrawShapes;

public record RectShape(Vector2 Position, Vector2 Size) : IDrawShape
{

    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawRect(Position.X, Position.Y, Size.X, Size.Y, paint);
    }
}