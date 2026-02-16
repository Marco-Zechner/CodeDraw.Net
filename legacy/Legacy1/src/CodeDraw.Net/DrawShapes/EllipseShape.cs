using MarcoZechner.MathDotNet;
using SkiaSharp;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.DrawShapes;

public record EllipseShape(Vector2 Center, Vector2 Size) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawOval(Center.X, Center.Y, Size.X, Size.Y, paint);
    }
}