using MarcoZechner.MathDotNet;
using SkiaSharp;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.DrawShapes;

public record PointShape(Vector2 Position) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawPoint(Position.X, Position.Y, paint);
    }
}